using Unity.Netcode.Components;

// Owner-authoritative NetworkTransform: whichever client currently owns the
// NetworkObject drives position/rotation. Combine with NetworkGrabbable so
// ownership transfers on grab — then the grabbing player moves the object
// and everyone else sees it smoothly (no server-snap-back).
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;
}
