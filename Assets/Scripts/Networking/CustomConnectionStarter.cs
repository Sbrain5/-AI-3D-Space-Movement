using PurrLobby;
using PurrNet;
using PurrNet.Steam;
using PurrNet.Transports;
using Steamworks;
using System.Collections;
using UnityEngine;

/// <summary>
/// CustomConnectionStarter handles starting the network connection
/// </summary>
public sealed class CustomConnectionStarter : MonoBehaviour
{
    #region Inspector Fields

    [Header("Timing")]
    [SerializeField] private float waitForLobbySeconds = 3f;

    [SerializeField] private float hostClientStartDelaySeconds = 1f;

    #endregion

    #region References

    private NetworkManager networkManager;
    private SteamTransport steamTransport;
    private UDPTransport udpTransport;
    private LobbyDataHolder lobbyDataHolder;

    #endregion

    #region State

    private bool started;

    #endregion

    #region Unity

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();
        steamTransport = GetComponent<SteamTransport>();
        udpTransport = GetComponent<UDPTransport>();
    }

    private void Start()
    {
        if (started)
        {
            return;
        }

        if (networkManager == null)
        {
            Debug.LogError("CustomConnectionStarter: NetworkManager component is missing.");
            return;
        }

        if (steamTransport == null)
        {
            Debug.LogError("CustomConnectionStarter: SteamTransport component is missing.");
            return;
        }

        if (udpTransport == null)
        {
            Debug.LogError("CustomConnectionStarter: UDPTransport component is missing.");
            return;
        }

        started = true;
        StartCoroutine(BeginConnection());
    }

    #endregion

    #region Flow

    /// <summary>
    /// Waits briefly for LobbyDataHolder, then starts either lobby flow (Steam) or normal flow (UDP).
    /// </summary>
    /// <returns> IEnumerator </returns>
    private IEnumerator BeginConnection()
    {
        yield return StartCoroutine(FindLobbyDataHolder());

        if (lobbyDataHolder != null && lobbyDataHolder.CurrentLobby.IsValid)
        {
            yield return StartCoroutine(StartFromLobby());
            yield break;
        }

        StartFromNormal();
    }

    /// <summary>
    /// Tries to find LobbyDataHolder for a short period.
    /// </summary>
    /// <returns> IEnumerator </returns>
    private IEnumerator FindLobbyDataHolder()
    {
        float elapsed = 0f;

        while (lobbyDataHolder == null && elapsed < waitForLobbySeconds)
        {
            lobbyDataHolder = FindFirstObjectByType<LobbyDataHolder>();

            if (lobbyDataHolder != null)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// Starts networking using SteamTransport and connects to the lobby owner.
    /// </summary>
    /// <returns> IEnumerator </returns>
    private IEnumerator StartFromLobby()
    {
        networkManager.transport = steamTransport;

        string lobbyIdText = lobbyDataHolder.CurrentLobby.LobbyId;

        ulong lobbyId;
        if (!ulong.TryParse(lobbyIdText, out lobbyId))
        {
            Debug.LogError("CustomConnectionStarter: Could not parse LobbyId. Falling back to UDP.");
            StartFromNormal();
            yield break;
        }

        CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(new CSteamID(lobbyId));
        if (!lobbyOwner.IsValid())
        {
            Debug.LogError("CustomConnectionStarter: Lobby owner SteamID is not valid. Falling back to UDP.");
            StartFromNormal();
            yield break;
        }

        steamTransport.address = lobbyOwner.ToString();

        if (lobbyDataHolder.CurrentLobby.IsOwner)
        {
            networkManager.StartServer();
        }

        yield return new WaitForSeconds(hostClientStartDelaySeconds);

        networkManager.StartClient();
    }

    /// <summary>
    /// Starts networking using UDPTransport.
    /// </summary>
    private void StartFromNormal()
    {
        networkManager.transport = udpTransport;

        Debug.Log("CustomConnectionStarter: Starting from normal flow (UDP).");

        networkManager.StartClient();
        Debug.Log("CustomConnectionStarter: Client started.");
    }

    #endregion
}