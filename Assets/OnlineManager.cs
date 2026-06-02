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
/// multi-player online match manager backed by Photon PUN 2.
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
    public event Action<bool, string> OnMatchResult;
    /// <summary>Fired when player count changes. Argument is new player count.</summary>
    public event Action<int> OnPlayerCountChanged;
    /// <summary>Fired when both players are ready. Argument is the level index to load.</summary>
    public event Action<int> OnMatchStarting;

    private string _roomCode;
    private int _levelIndex;
    private bool _isHost;
    private bool _matchActive;
    private bool _pendingCreate;
    private bool _pendingJoin;
    private bool _isMatchmaking;

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

    /// <summary>Find a random opponent via Photon matchmaking (no room code needed).</summary>
    public void FindMatch()
    {
#if PHOTON_UNITY_NETWORKING
        if (State != MatchState.Idle) return;
        _isMatchmaking = true;
        _isHost = false;
        _pendingCreate = false;
        _pendingJoin = false;
        _roomCode = GenerateCode();

        SetState(MatchState.Connecting);
        OnStatusMessage?.Invoke("Searching for opponent…");

        if (PhotonNetwork.IsConnectedAndReady) DoFindMatch();
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
        _isMatchmaking = false;
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
        else if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
#endif
        SetState(MatchState.Idle);
    }

    /// <summary>Stay in the same room and start a new round with a different level.</summary>
    public void StartRematch()
    {
#if PHOTON_UNITY_NETWORKING
        if (!PhotonNetwork.InRoom) { LeaveMatch(); return; }
        _matchActive = false;
        _isMatchmaking = false;
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.CurrentRoom.IsOpen = true;
            int newLvl;
            do { newLvl = UnityEngine.Random.Range(0, 300); } while (newLvl == _levelIndex);
            var props = new Hashtable { { PROP_LEVEL, newLvl }, { PROP_STATE, "waiting" } };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }
        SetState(MatchState.WaitingForOpponent);
        OnPlayerCountChanged?.Invoke(PhotonNetwork.CurrentRoom.PlayerCount);
#else
        LeaveMatch();
#endif
    }

    /// <summary>Host starts the match for everyone in the room.</summary>
    public void StartGame()
    {
#if PHOTON_UNITY_NETWORKING
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
        if (PhotonNetwork.CurrentRoom.PlayerCount < 1) return;
        int lvl = UnityEngine.Random.Range(0, 300);
        var props = new Hashtable { { PROP_LEVEL, lvl }, { PROP_STATE, "starting" } };
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
#endif
    }

    public int GetLevelIndex() => _levelIndex;
    public bool IsMatchmaking => _isMatchmaking;

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
            MaxPlayers = 8,
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

    private void DoFindMatch()
    {
        SetState(MatchState.JoiningRoom);
        OnStatusMessage?.Invoke("Searching for opponent…");
        var expectedProps = new Hashtable { { PROP_STATE, "waiting" } };
        PhotonNetwork.JoinRandomRoom(expectedProps, 0);
    }

    private void DoCreateMatchmakingRoom()
    {
        SetState(MatchState.CreatingRoom);
        OnStatusMessage?.Invoke("Waiting for opponent…");
        var opts = new RoomOptions
        {
            MaxPlayers = 8,
            IsVisible = true,
            IsOpen = true,
            CustomRoomProperties = new Hashtable { { PROP_LEVEL, -1 }, { PROP_STATE, "waiting" } },
            CustomRoomPropertiesForLobby = new[] { PROP_STATE }
        };
        PhotonNetwork.CreateRoom("MM_" + _roomCode, opts);
    }

    // ── IConnectionCallbacks ──────────────────────────────

    void IConnectionCallbacks.OnConnected() { }

    void IConnectionCallbacks.OnConnectedToMaster()
    {
        if (_pendingCreate) DoCreateRoom();
        else if (_pendingJoin) DoJoinRoom();
        else if (_isMatchmaking) DoFindMatch();
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
        _roomCode = GenerateCode();
        if (_isMatchmaking) DoCreateMatchmakingRoom();
        else DoCreateRoom();
    }

    void IMatchmakingCallbacks.OnJoinedRoom()
    {
        _roomCode = PhotonNetwork.CurrentRoom.Name;
        OnRoomCodeReady?.Invoke(_roomCode);
        SetState(MatchState.WaitingForOpponent);
        OnStatusMessage?.Invoke("Waiting for host to start…");
        OnPlayerCountChanged?.Invoke(PhotonNetwork.CurrentRoom.PlayerCount);
    }

    void IMatchmakingCallbacks.OnJoinRoomFailed(short returnCode, string message)
    {
        OnStatusMessage?.Invoke("Room not found. Check the code and try again.");
        SetState(MatchState.Idle);
    }

    void IMatchmakingCallbacks.OnLeftRoom() { SetState(MatchState.Idle); }
    void IMatchmakingCallbacks.OnJoinRandomFailed(short returnCode, string message)
    {
        if (_isMatchmaking)
        {
            _isHost = true;
            DoCreateMatchmakingRoom();
        }
    }
    void IMatchmakingCallbacks.OnFriendListUpdate(List<FriendInfo> friendList) { }

    // ── IInRoomCallbacks ──────────────────────────────────

    void IInRoomCallbacks.OnPlayerEnteredRoom(Player newPlayer)
    {
        OnPlayerCountChanged?.Invoke(PhotonNetwork.CurrentRoom.PlayerCount);
        // Auto-start immediately for matchmaking when 2+ players join
        if (_isMatchmaking && PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount >= 2)
            StartGame();
    }

    void IInRoomCallbacks.OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (!propertiesThatChanged.ContainsKey(PROP_STATE)) return;
        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        var st = (string)props[PROP_STATE];
        if (st == "starting")
        {
            _levelIndex = (int)props[PROP_LEVEL];
            _matchActive = true;
            SetState(MatchState.InMatch);
            OnMatchStarting?.Invoke(_levelIndex);
        }
        else if (st == "waiting")
        {
            SetState(MatchState.WaitingForOpponent);
            OnStatusMessage?.Invoke("Waiting for host to start…");
            OnPlayerCountChanged?.Invoke(PhotonNetwork.CurrentRoom.PlayerCount);
        }
    }

    void IInRoomCallbacks.OnPlayerLeftRoom(Player otherPlayer)
    {
        if (_matchActive) OnStatusMessage?.Invoke("A player disconnected.");
        OnPlayerCountChanged?.Invoke(PhotonNetwork.CurrentRoom.PlayerCount);
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
        // Get winner display name
        var winner = PhotonNetwork.CurrentRoom.GetPlayer(winnerActor);
        string winnerName = (winner != null && !string.IsNullOrEmpty(winner.NickName))
            ? winner.NickName
            : $"Player {winnerActor}";
        OnMatchResult?.Invoke(iWon, winnerName);
    }

    void OnEnable()  { PhotonNetwork.AddCallbackTarget(this); }
    void OnDisable() { PhotonNetwork.RemoveCallbackTarget(this); }

#endif // PHOTON_UNITY_NETWORKING
}
