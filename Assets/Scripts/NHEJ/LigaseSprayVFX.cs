using UnityEngine;

// Self-configuring spray VFX for the LigaseIV spray can.
// Attach to an empty GameObject and save as a prefab.
// Assign that prefab to LigaseSprayCan.sprayVFXPrefab.
//
// No manual particle tweaking needed — everything is built in Awake().
// The effect plays once and the GameObject is destroyed by LigaseSprayCan after 2s.
//
// Two-layer effect:
//   1. Spray cone  — fast, thin stream of bright particles shooting forward
//   2. Mist cloud  — slower, softer particles that billow outward on impact
[RequireComponent(typeof(ParticleSystem))]
public class LigaseSprayVFX : MonoBehaviour
{
    [Header("Colour — change to match your art direction")]
    [SerializeField] Color sprayColour = new Color(0.2f, 1f, 0.5f, 1f);   // bright green
    [SerializeField] Color mistColour  = new Color(0.1f, 0.8f, 0.4f, 0.4f); // soft translucent green

    [Header("Scale")]
    [SerializeField] float spraySpeed    = 2.5f;
    [SerializeField] float particleSize  = 1f;

    [Header("Direction")]
    [Tooltip("Rotate the cone shape until particles point in the right direction. Try X: 0, -90, or 90.")]
    [SerializeField] Vector3 shapeRotationEuler = new Vector3(90f, 0f, 0f);

    ParticleSystem sprayPS;
    ParticleSystem mistPS;

    void Awake()
    {
        // ── Layer 1: spray cone ───────────────────────────────────────────────
        sprayPS = GetComponent<ParticleSystem>();
        ConfigureSprayCone(sprayPS);

        // Mist cloud disabled — sphere shape caused explosion-like spread.
        // mistPS intentionally left null.
    }

    void Start()
    {
        // Rotate the cone shape so it points along transform.forward.
        // The cone emits along its own local +Y, so we find the rotation
        // that maps +Y onto the world forward direction of this VFX object.
        var sprayShape      = sprayPS.shape;
        sprayShape.rotation = shapeRotationEuler;

        sprayPS.Play();
    }

    // ── Particle system builders ──────────────────────────────────────────────

    void ConfigureSprayCone(ParticleSystem ps)
    {
        // Main module
        var main          = ps.main;
        main.duration     = 1f;
        main.loop         = true;
        main.startLifetime = 0.3f;
        main.startSpeed   = new ParticleSystem.MinMaxCurve(spraySpeed * 0.8f, spraySpeed * 1.2f);
        main.startSize    = new ParticleSystem.MinMaxCurve(particleSize * 0.5f, particleSize);
        main.startColor   = new ParticleSystem.MinMaxGradient(sprayColour,
                                sprayColour * new Color(0.8f, 0.8f, 0.8f, 0.6f));
        main.maxParticles = 200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        // Emission — burst of particles immediately
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 60f;

        // Shape — narrow cone pointing forward (local +Z)
        var shape    = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle  = 8f;
        shape.radius = 0.01f;

        // Colour over lifetime — fade out at the end
        var col      = ps.colorOverLifetime;
        col.enabled  = true;
        var grad     = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color    = new ParticleSystem.MinMaxGradient(grad);

        // Size over lifetime — shrink toward tip
        var size     = ps.sizeOverLifetime;
        size.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.5f, 0.6f), new Keyframe(1f, 0.1f));
        size.size    = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Renderer — use default additive-style material
        var rend     = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.material   = CreateParticleMaterial(sprayColour);
    }

// ── Material helper ───────────────────────────────────────────────────────

    static Material CreateParticleMaterial(Color tint)
    {
        // Uses URP Particles/Additive if available, falls back to built-in Additive.
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Additive")
                     ?? Shader.Find("Sprites/Default");

        var mat       = new Material(shader);
        mat.color     = tint;

        // URP blend mode for soft additive look
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f);   // transparent
            mat.SetFloat("_Blend", 2f);     // additive
        }

        return mat;
    }
}
