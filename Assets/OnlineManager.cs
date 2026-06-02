using System;
using System.Collections.Generic;
using UnityEngine;

#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using Hashtable = ExitGames.Client.Photon.Hashtable;
#endif

/// <summary>
/// 1v1 online match manager backed by Photon PUN 2.
/// Install Photon PUN 2 from the Unity Asset Store to activate.
/// After installing, open Tools → Photon Unity Networking → PUN Wizard and enter your App ID.
/// </summary>
public class OnlineManager : MonoBehaviour
#if PHOTON_UNITY_NETWORKING
    , IConnectionCallbacks, IMatchmakingCallbacks, IInRoomCallbacks, IOnEventCallback
#endif
{
    public static OnlineManager Instance { get; private set; }

    public enum MatchState
    {
        Idle,
        Connecting,
        CreatingRoom,
        JoiningRoom,
        WaitingForOpponent,
        InMatch,
        Finished
    }

    public MatchState State { get; private set; } = MatchState.Idle;

    /// <summary>Fired whenever the match state changes.</summary>
    public event Action<MatchState> OnStateChanged;
    /// <summary>Human-readable status message for the UI.</summary>
    public event Action<string> OnStatusMessage;
    /// <summary>Fired when the room is ready and the code is known (host and guest).</summary>
    public event Action<string> OnRoomCodeReady;
    /// <summary>Fired when the match result is determined. True = local player won.</summary>
    public event Action<bool> OnMatchResult;
    /// <summary>Fired when both players are ready. Argument is the level index to load.</summary>
    public event Action<int> OnMatchStarting;

    private string _roomCode;
    private int _levelIndex;
    private bool _isHost;
    private bool _matchActive;
    private bool _pendingCreate;
    private bool _pendingJoin;

    // Room custom property keys (kept short for bandwidth)
    private const string PROP_LEVEL = "lv";
    private const string PROP_STATE = "st";

    // Event code for "I finished"
    private const byte EVT_DONE = 1;

    private static readonly char[] CODE_CHARS = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    // ──────────────────────────────────────────────────────
    //  Unity lifecycle
    // ──────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ──────────────────────────────────────────────────────
    //  Public API  (no Photon types; safe to call without PUN)
    // ──────────────────────────────────────────────────────

    public void CreateRoom()
    {
#if PHOTON_UNITY_NETWORKING
        if (State != MatchState.Idle) return;
        _isHost = true;
        _pendingCreate = true;
        _pendingJoin = false;
        _roomCode = GenerateCode();

        SetState(MatchState.Connecting);
        OnStatusMessage?.Invoke("Connecting to server…");

        if (PhotonNetwork.IsConnectedAndReady) DoCreateRoom();
        else PhotonNetwork.ConnectUsingSettings();
#else
        OnStatusMessage?.Invoke("Install Photon PUN 2 from the Asset Store to use online mode.");
#endif
    }

    public void JoinRoom(string code)
    {
#if PHOTON_UNITY_NETWORKING
        if (State != MatchState.Idle) return;
        if (string.IsNullOrWhiteSpace(code)) { OnStatusMessage?.Invoke("Enter a room code first."); return; }
        _isHost = false;
        _pendingCreate = false;
        _pendingJoin = true;
        _roomCode = code.Trim().ToUpperInvariant();

        SetState(MatchState.Connecting);
        OnStatusMessage?.Invoke("Connecting to server…");

        if (PhotonNetwork.IsConnectedAndReady) DoJoinRoom();
        else PhotonNetwork.ConnectUsingSettings();
#else
        OnStatusMessage?.Invoke("Install Photon PUN 2 from the Asset Store to use online mode.");
#endif
    }

    /// <summary>Call this when the local player completes the puzzle during an online match.</summary>
    public void NotifyLevelFinished()
    {
#if PHOTON_UNITY_NETWORKING
        if (!_matchActive) return;
        _matchActive = false;

        // First one to raise this event is declared the winner.
        // ReceiverGroup.All ensures the sender also receives it, so winner determination
        // happens identically on both clients.
        var data = new object[] { PhotonNetwork.LocalPlayer.ActorNumber };
        PhotonNetwork.RaiseEvent(
            EVT_DONE, data,
            new RaiseEventOptions { Receivers = ReceiverGroup.All },
            SendOptions.SendReliable);
#endif
    }

    public void LeaveMatch()
    {
#if PHOTON_UNITY_NETWORKING
        _matchActive = false;
        _pendingCreate = _pendingJoin = false;
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        else if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
#endif
        SetState(MatchState.Idle);
    }

    public int GetLevelIndex() => _levelIndex;

    // ──────────────────────────────────────────────────────
    //  Private helpers
    // ──────────────────────────────────────────────────────

    private void SetState(MatchState s) { State = s; OnStateChanged?.Invoke(s); }

    private string GenerateCode()
    {
        var sb = new System.Text.StringBuilder(6);
        for (int i = 0; i < 6; i++) sb.Append(CODE_CHARS[UnityEngine.Random.Range(0, CODE_CHARS.Length)]);
        return sb.ToString();
    }

#if PHOTON_UNITY_NETWORKING

    private void DoCreateRoom()
    {
        SetState(MatchState.CreatingRoom);
        OnStatusMessage?.Invoke("Creating room…");
        var opts = new RoomOptions
        {
            MaxPlayers = 2,
            IsVisible = false,
            IsOpen = true,
            CustomRoomProperties = new Hashtable { { PROP_LEVEL, -1 }, { PROP_STATE, "waiting" } },
            CustomRoomPropertiesForLobby = new[] { PROP_LEVEL, PROP_STATE }
        };
        PhotonNetwork.CreateRoom(_roomCode, opts);
    }

    private void DoJoinRoom()
    {
        SetState(MatchState.JoiningRoom);
        OnStatusMessage?.Invoke("Joining room…");
        PhotonNetwork.JoinRoom(_roomCode);
    }

    private void TryStartMatch()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (PhotonNetwork.CurrentRoom.PlayerCount < 2) return;

        // Levels 0–299: square & hex shapes — good for quick 1v1
        int lvl = UnityEngine.Random.Range(0, 300);
        var props = new Hashtable { { PROP_LEVEL, lvl }, { PROP_STATE, "starting" } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        PhotonNetwork.CurrentRoom.IsOpen = false;
    }

    // ── IConnectionCallbacks ──────────────────────────────

    void IConnectionCallbacks.OnConnected() { }

    void IConnectionCallbacks.OnConnectedToMaster()
    {
        if (_pendingCreate) DoCreateRoom();
        else if (_pendingJoin) DoJoinRoom();
    }

    void IConnectionCallbacks.OnDisconnected(DisconnectCause cause)
    {
        if (State != MatchState.Idle)
            OnStatusMessage?.Invoke($"Connection lost: {cause}");
        SetState(MatchState.Idle);
    }

    void IConnectionCallbacks.OnRegionListReceived(RegionHandler regionHandler) { }
    void IConnectionCallbacks.OnCustomAuthenticationResponse(Dictionary<string, object> data) { }
    void IConnectionCallbacks.OnCustomAuthenticationFailed(string debugMessage) { }

    // ── IMatchmakingCallbacks ──────────────────────────────

    void IMatchmakingCallbacks.OnCreatedRoom() { }

    void IMatchmakingCallbacks.OnCreateRoomFailed(short returnCode, string message)
    {
        // Code collision — try a fresh one
        _roomCode = GenerateCode();
        DoCreateRoom();
    }

    void IMatchmakingCallbacks.OnJoinedRoom()
    {
        OnRoomCodeReady?.Invoke(_roomCode);
        if (PhotonNetwork.CurrentRoom.PlayerCount >= 2)
            TryStartMatch();
        else
        {
            SetState(MatchState.WaitingForOpponent);
            OnStatusMessage?.Invoke("Waiting for opponent…");
        }
    }

    void IMatchmakingCallbacks.OnJoinRoomFailed(short returnCode, string message)
    {
        OnStatusMessage?.Invoke("Room not found. Check the code and try again.");
        SetState(MatchState.Idle);
    }

    void IMatchmakingCallbacks.OnLeftRoom() { SetState(MatchState.Idle); }
    void IMatchmakingCallbacks.OnJoinRandomFailed(short returnCode, string message) { }
    void IMatchmakingCallbacks.OnFriendListUpdate(List<FriendInfo> friendList) { }
    void IMatchmakingCallbacks.OnJoinedLobby() { }
    void IMatchmakingCallbacks.OnLeftLobby() { }
    void IMatchmakingCallbacks.OnRoomListUpdate(List<RoomInfo> roomList) { }

    // ── IInRoomCallbacks ──────────────────────────────────

    void IInRoomCallbacks.OnPlayerEnteredRoom(Player newPlayer) { TryStartMatch(); }

    void IInRoomCallbacks.OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (!propertiesThatChanged.ContainsKey(PROP_STATE)) return;
        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        if ((string)props[PROP_STATE] != "starting") return;

        _levelIndex = (int)props[PROP_LEVEL];
        _matchActive = true;
        SetState(MatchState.InMatch);
        OnMatchStarting?.Invoke(_levelIndex);
    }

    void IInRoomCallbacks.OnPlayerLeftRoom(Player otherPlayer)
    {
        if (_matchActive) OnStatusMessage?.Invoke("Opponent disconnected.");
    }

    void IInRoomCallbacks.OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) { }
    void IInRoomCallbacks.OnMasterClientSwitched(Player newMasterClient) { }

    // ── IOnEventCallback ──────────────────────────────────

    void IOnEventCallback.OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != EVT_DONE) return;

        var data = (object[])photonEvent.CustomData;
        int winnerActor = (int)data[0];
        bool iWon = winnerActor == PhotonNetwork.LocalPlayer.ActorNumber;
        _matchActive = false;
        SetState(MatchState.Finished);
        OnMatchResult?.Invoke(iWon);
    }

    void OnEnable()  { PhotonNetwork.AddCallbackTarget(this); }
    void OnDisable() { PhotonNetwork.RemoveCallbackTarget(this); }

#endif // PHOTON_UNITY_NETWORKING
}
