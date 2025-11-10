using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Check this out we can require components be on a game object!
[RequireComponent(typeof(MeshFilter))]

public class BParticleSimMesh : MonoBehaviour
{
    public struct BSpring
    {
        public float kd;                        // damping coefficient
        public float ks;                        // spring coefficient
        public float restLength;                // rest length of this spring
        public int attachedParticle;            // index of the attached other particle (use me wisely to avoid doubling springs and sprign calculations)
    }

    public struct BContactSpring
    {
        public float kd;                        // damping coefficient
        public float ks;                        // spring coefficient
        public float restLength;                // rest length of this spring (think about this ... may not even be needed o_0
        public Vector3 attachPoint;             // the attached point on the contact surface
    }

    public struct BParticle
    {
        public Vector3 position;                // position information
        public Vector3 velocity;                // velocity information
        public float mass;                      // mass information
        public BContactSpring contactSpring;    // Special spring for contact forces
        public bool attachedToContact;          // is thi sparticle currently attached to a contact (ground plane contact)
        public List<BSpring> attachedSprings;   // all attached springs, as a list in case we want to modify later fast
        public Vector3 currentForces;           // accumulate forces here on each step        
    }

    public struct BPlane
    {
        public Vector3 position;                // plane position
        public Vector3 normal;                  // plane normal
    }

    public float contactSpringKS = 1000.0f;     // contact spring coefficient with default 1000
    public float contactSpringKD = 20.0f;       // contact spring daming coefficient with default 20

    public float defaultSpringKS = 100.0f;      // default spring coefficient with default 100
    public float defaultSpringKD = 1.0f;        // default spring daming coefficient with default 1

    public bool debugRender = true;            // To render or not to render


    /*** 
     * I've given you all of the above to get you started
     * Here you need to publicly provide the:
     * - the ground plane transform (Transform)
     * - handlePlaneCollisions flag (bool)
     * - particle mass (float)
     * - useGravity flag (bool)
     * - gravity value (Vector3)
     * Here you need to privately provide the:
     * - Mesh (Mesh)
     * - array of particles (BParticle[])
     * - the plane (BPlane)
     ***/

    public Transform groundPlane;
    public bool handlePlaneCollisions;
    public float mass;
    public bool useGravity;
    public Vector3 gravity;


    private Mesh mesh;
    private BParticle[] particles;
    private BPlane bPlane;


    /// <summary>
    /// Init everything
    /// HINT: in particular you should probbaly handle the mesh, init all the particles, and the ground plane
    /// HINT 2: I'd for organization sake put the init particles and plane stuff in respective functions
    /// HINT 3: Note that mesh vertices when accessed from the mesh filter are in local coordinates.
    ///         This script will be on the object with the mesh filter, so you can use the functions
    ///         transform.TransformPoint and transform.InverseTransformPoint accordingly 
    ///         (you need to operate on world coordinates, and render in local)
    /// HINT 4: the idea here is to make a mathematical particle object for each vertex in the mesh, then connect
    ///         each particle to every other particle. Be careful not to double your springs! There is a simple
    ///         inner loop approach you can do such that you attached exactly one spring to each particle pair
    ///         on initialization. Then when updating you need to remember a particular trick about the spring forces
    ///         generated between particles. 
    /// </summary>

    void InitParticles()
    {
        List<Vector3> vertices = new List<Vector3>();
        mesh.GetVertices(vertices);
        int numVertices = vertices.Count;
        particles = new BParticle[numVertices];

        // Initialize particles for each vertex
        for (int i = 0; i < numVertices; i++)
        {
            BParticle curPart = new BParticle 
            { 
                position = transform.TransformPoint(vertices[i]),
                velocity = Vector3.zero,
                mass = mass,
                contactSpring = { ks = contactSpringKS, kd = contactSpringKD, restLength = 0, attachPoint = Vector3.zero },
                attachedToContact = false,
                attachedSprings = new List<BSpring>(),
                currentForces = Vector3.zero
            };
            particles[i] = curPart;
        }

        // Now connect each particle with springs
        for (int i = 0; i < particles.Length; i++)
        {   for (int j = i + 1; j < vertices.Count; j++)
            {
                BSpring curSpring = new BSpring
                {
                    kd = defaultSpringKD,
                    ks = defaultSpringKS,
                    restLength = Vector3.Distance(particles[i].position, particles[j].position),
                    attachedParticle = j
                };
                particles[i].attachedSprings.Add(curSpring);
            }
        }
    }

    void InitPlane()
    {
        bPlane = new BPlane { position = groundPlane.transform.position, normal = groundPlane.transform.up };
    }

    public void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        InitParticles();
        InitPlane();
    }



    /*** BIG HINT: My solution code has as least the following functions
     * InitParticles()
     * InitPlane()
     * UpdateMesh() (remember the hint above regarding global and local coords)
     * ResetParticleForces()
     * ...
     ***/



    /// <summary>
    /// Draw a frame with some helper debug render code
    /// </summary>
    public void Update()
    {
        /* This will work if you have a correctly made particles array */
        if (debugRender)
        {
            int particleCount = particles.Length;
            for (int i = 0; i < particleCount; i++)
            {
                Debug.DrawLine(particles[i].position, particles[i].position + particles[i].currentForces, Color.blue);

                int springCount = particles[i].attachedSprings.Count;
                for (int j = 0; j < springCount; j++)
                {
                    Debug.DrawLine(particles[i].position, particles[particles[i].attachedSprings[j].attachedParticle].position, Color.red);
                }
            }
        }
    }


    void SetContactSprings()
    {
        int particleCount = particles.Length;
        for (int i = 0; i < particleCount; i++)
        {
            BParticle p = particles[i];
            if (Vector3.Dot(p.position - bPlane.position, bPlane.normal) <= 0 && !p.attachedToContact)
            {
                // Find attach point via projection to plane
                Vector3 projection = p.position - Vector3.Dot(p.position - bPlane.position, bPlane.normal) * bPlane.normal;
                p.contactSpring.attachPoint = projection;
                p.attachedToContact = true;
            }
            else 
            {
                p.attachedToContact = false;
            }
            particles[i] = p;
        }
    }

    // Return the force to apply to the local particle from the particle-particle spring
    Vector3 GetParticleSpringForce(BSpring spring, BParticle local)
    {
        // Refer to fancy calculation...
        BParticle other = particles[spring.attachedParticle];
        Vector3 posDiff = local.position - other.position;      if (posDiff == Vector3.zero) { return Vector3.zero; }
        Vector3 velDiff = local.velocity - other.velocity;
        Vector3 posDNml = posDiff / posDiff.magnitude;

        Vector3 contact = (spring.restLength - posDiff.magnitude) * posDNml;
        Vector3 damp    = Vector3.Dot(velDiff, posDNml) * posDNml;
        return spring.ks * contact - spring.kd * damp;
    }

    // Return the force to apply to the local particle from the particle-ground spring
    Vector3 GetContactSpringForce(BContactSpring spring, BParticle local)
    {
        // Refer to fancy calculation 
        Vector3 contact = Vector3.Dot(local.position - spring.attachPoint, bPlane.normal) * bPlane.normal;
        return -spring.ks * contact - spring.kd * local.velocity;
    }

    // Reset forces.. also set them again :)
    void ResetParticleForces()
    {
        for (int i = 0; i < particles.Length; i++)
        { particles[i].currentForces = gravity; }

        for (int i = 0; i < particles.Length; i++)
        {
            BParticle p = particles[i];

            // Calculate p-p spring force, then apply appropriately to each particle
            for (int j = 0; j < p.attachedSprings.Count; j++)
            {
                BSpring spring = p.attachedSprings[j];
                Vector3 f = GetParticleSpringForce(spring, p);
                p.currentForces += f;
                particles[spring.attachedParticle].currentForces -= f;
            }

            // Do contact spring stuff if needed
            if (p.attachedToContact)
            {
                p.currentForces += GetContactSpringForce(p.contactSpring, p);
            }
            particles[i] = p;
        }

    }

    // Update the velocity and position
    // vi = v0 + dt * F(x0, v0, t0)/mi
    // xi = x0 + dt * vi
    void SymplecticEulerUpdate(float dt)
    {
        int particleCount = particles.Length;
        for (int i = 0; i < particleCount; i++)
        {
            BParticle p = particles[i];
            p.velocity += dt * p.currentForces / p.mass;
            p.position += dt * p.velocity;
            particles[i] = p;
        }
    }

    // Update the mesh with particle info
    void UpdateMesh()
    {
        List<Vector3> newVerts = new List<Vector3>();
        for (int i = 0; i < particles.Length; i++)
        {
            newVerts.Add(transform.InverseTransformPoint(particles[i].position));
        }
        mesh.SetVertices(newVerts);
        //mesh.RecalculateNormals();
        //mesh.RecalculateBounds();
    }

    void FixedUpdate() 
    {
        SetContactSprings();
        ResetParticleForces();
        SymplecticEulerUpdate(Time.fixedDeltaTime);
        UpdateMesh();
    }
}
