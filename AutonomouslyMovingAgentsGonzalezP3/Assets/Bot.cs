using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Bot : MonoBehaviour
{
    NavMeshAgent agent;
    public GameObject target;
    Drive ds;

    void Start()
    {
        agent = this.GetComponent<NavMeshAgent>();
        ds = target.GetComponent<Drive>();
    }

    void Seek(Vector3 location)
    {
        agent.SetDestination(location);
    }

    void Flee(Vector3 location)
    {
        Vector3 fleeVector = location - this.transform.position;
        agent.SetDestination(this.transform.position - fleeVector);
    }
    void Pursue()
    {
        Vector3 targetDir = target.transform.position - this.transform.position;

        float relativeHeading = Vector3.Angle(this.transform.forward, this.transform.TransformVector(target.transform.forward));

        float toTarget = Vector3.Angle(this.transform.forward, this.transform.TransformVector(targetDir));

        if ((toTarget > 90 && relativeHeading < 20) || ds.currentSpeed < 0.01f)
        {
            Seek(target.transform.position);
            return;
        }

        float lookAhead = targetDir.magnitude / (agent.speed + ds.currentSpeed);
        Seek(target.transform.position + target.transform.forward * lookAhead);
    }

    void Evade()
    {
        Vector3 targetDir = target.transform.position - this.transform.position;
        float lookAhead = targetDir.magnitude / (agent.speed + ds.currentSpeed);

        Flee(target.transform.position + target.transform.forward * lookAhead);
    }
    Vector3 wanderTarget = Vector3.zero;
    void Wander()
    {
        float wanderRadius = 10;
        float wanderDistance = 10;
        float wanderJitter = 1;

        wanderTarget += new Vector3(Random.Range(-1.0f, 1.0f) * wanderJitter,
                                        0,
                                        Random.Range(-1.0f, 1.0f) * wanderJitter);
        wanderTarget.Normalize();
        wanderTarget *= wanderRadius;

        Vector3 targetLocal = wanderTarget + new Vector3(0, 0, wanderDistance);
        Vector3 targetWorld = this.gameObject.transform.InverseTransformVector(targetLocal);

        Seek(targetWorld);
    }

    void Hide()
    {
        float dist = Mathf.Infinity;
        Vector3 chosenSpot = Vector3.zero;
        for (int i = 0; i < World.Instance.GetHidingSpots().Length; i++)
        {
            //determine the direction of the hiding spot from the target
            Vector3 hideDir = World.Instance.GetHidingSpots()[i].transform.position - target.transform.position;

            //add this direction to the position of the hiding spot to find a location on the
            //opposite side of the hiding spot to where the target is
            Vector3 hidePos = World.Instance.GetHidingSpots()[i].transform.position + hideDir.normalized * 10;

            //if this hiding spot is closer to the agent than the distance to the last one
            if (Vector3.Distance(this.transform.position, hidePos) < dist)
            {
                //remember it
                chosenSpot = hidePos;
                dist = Vector3.Distance(this.transform.position, hidePos);
            }
        }

        //go to the hiding location
        Seek(chosenSpot);

    }

    //look for a hiding spot but determine where the agent must stand
    //based on the boundary of the object determined by a box collider
    void CleverHide()
    {
        float dist = Mathf.Infinity;
        Vector3 chosenSpot = Vector3.zero;
        Vector3 chosenDir = Vector3.zero;
        GameObject chosenGO = World.Instance.GetHidingSpots()[0];

        //same logic as for Hide() to find the closest hiding spot
        for (int i = 0; i < World.Instance.GetHidingSpots().Length; i++)
        {
            Vector3 hideDir = World.Instance.GetHidingSpots()[i].transform.position - target.transform.position;
            Vector3 hidePos = World.Instance.GetHidingSpots()[i].transform.position + hideDir.normalized * 100;

            if (Vector3.Distance(this.transform.position, hidePos) < dist)
            {
                chosenSpot = hidePos;
                chosenDir = hideDir;
                chosenGO = World.Instance.GetHidingSpots()[i];
                dist = Vector3.Distance(this.transform.position, hidePos);
            }
        }

        //get the collider of the chosen hiding spot
        Collider hideCol = chosenGO.GetComponent<Collider>();
        //calculate a ray to hit the hiding spot's collider from the opposite side to where
        //the target is located
        Ray backRay = new Ray(chosenSpot, -chosenDir.normalized);
        RaycastHit info;
        float distance = 250.0f;
        //perform a raycast to find the point near the array
        hideCol.Raycast(backRay, out info, distance);

        //go and stand at the back of the object at the ray hit point
        Seek(info.point + chosenDir.normalized);

    }

    //Can the agent see the target from where it is
    //based on other game objects in the world
    bool CanSeeTarget()
    {
        RaycastHit raycastInfo;
        //calculate a ray to the target from the agent
        Vector3 rayToTarget = target.transform.position - this.transform.position;
        //perform a raycast to determine if there's anything between the agent and the target
        if (Physics.Raycast(this.transform.position, rayToTarget, out raycastInfo))
        {
            //ray will hit the target if no other colliders in the way
            if (raycastInfo.transform.gameObject.tag == "cop")
                return true;
        }
        return false;
    }

    //Can the target potentially see the agent
    bool TargetCanSeeMe()
    {
        //work out a forward facing direction for the target and
        //the angle between that and the direction to the agent
        Vector3 toAgent = this.transform.position - target.transform.position;
        float lookingAngle = Vector3.Angle(target.transform.forward, toAgent);

        //if the target is facing the agent within 60 degrees of straight on
        //lets assume the target can see the agent
        if (lookingAngle < 60)
            return true;
        return false;
    }

    //provide a timed cool down boolean to allow agents time to get to a
    //nav mesh location before another location is potentially calculated
    bool coolDown = false;
    void BehaviourCoolDown()
    {
        coolDown = false;
    }

    //determine how far the target is from the agent
    bool TargetInRange()
    {
        //if the target is within 10 units of the agent then consider it within range to
        //affect the agent's behaviour
        if (Vector3.Distance(this.transform.position, target.transform.position) < 10)
            return true;
        return false;
    }

    // Update is called once per frame
    void Update()
    {
        //if not waiting for a cool down to finish
        if (!coolDown)
        {
            //if the target is considered out of range - e.g. not a threat
            if (!TargetInRange())
            {
                Wander();
            }
            else if (CanSeeTarget() && TargetCanSeeMe()) //if there's nothing between the agent and target
            {                                              //and the target is facing the agent
                CleverHide();                               //go hide behind something
                coolDown = true;
                Invoke("BehaviourCoolDown", 5);             //continue hiding for 5 seconds
            }
            else
                Pursue();   //otherwise pursue the target
        }
    }
}
