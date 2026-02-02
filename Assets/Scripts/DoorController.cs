using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Tilemaps;

public class DoorTilemapController : NetworkBehaviour
{
    [Header("Plates")]
    [SerializeField] private PressurePlate plateA;
    [SerializeField] private PressurePlate plateB;

    [Header("Door")]
    [SerializeField] private Tilemap doorTilemap;
    [SerializeField] private TileBase[] doorTiles;

    private readonly SyncVar<bool> _doorOpen = new();

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        _doorOpen.OnChange += OnDoorStateChanged;
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        _doorOpen.OnChange -= OnDoorStateChanged;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("DoorTilemapController OnStartServer fired");
        InvokeRepeating(nameof(ServerEvaluate), 0.1f, 0.1f);
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        CancelInvoke(nameof(ServerEvaluate));
    }

    [Server]
    private void ServerEvaluate()
    {
        bool shouldOpen = plateA.IsPressed && plateB.IsPressed;

        if (_doorOpen.Value != shouldOpen)
            _doorOpen.Value = shouldOpen;
    }

    private void OnDoorStateChanged(bool oldValue, bool newValue, bool asServer)
    {
        ApplyDoorState(newValue);
    }

    private void ApplyDoorState(bool open)
    {
        Debug.Log("Door state changed " + open);

        var bounds = doorTilemap.cellBounds;

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int pos = new(x, y, 0);
                TileBase tile = doorTilemap.GetTile(pos);
                if (tile == null) continue;

                if (IsDoorTile(tile))
                {
                    if (open)
                        doorTilemap.SetTile(pos, null);
                }
            }
        }

        doorTilemap.RefreshAllTiles();
    }

    private bool IsDoorTile(TileBase tile)
    {
        foreach (var dt in doorTiles)
            if (tile == dt) return true;
        return false;
    }
}
        