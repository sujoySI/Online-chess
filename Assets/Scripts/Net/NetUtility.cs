using System;
using UnityEngine;
using Unity.Networking.Transport;

public enum OpCode
{
    KEEP_ALIVE = 1,
    WLCOME = 2,
    START_GAME = 3,
    MAKE_MOVE = 4,
    REMATCH = 5
}
public static class NetUtility
{
    public static void OnData(DataStreamReader stream, NetworkConnection cnn, Server server = null)
    {
        NetMessages msg = null;
        var opCode = (OpCode)stream.ReadByte();
        switch(opCode)
        {
            case OpCode.KEEP_ALIVE: msg = new NetKeepAlive(stream);break;
            //case OpCode.WLCOME: msg = new NetWelcome(stream);break;
            //case OpCode.START_GAME: msg = NetStartGame(stream);break;
            //case OpCode.MAKE_MOVE: msg = NetMakeMove(stream);break;
            //case OpCode.REMATCH: msg = NetRematch(stream);break;
            default:
                Debug.LogError("Message Received had no OpCode");
                break;
        }

        if(server != null)
        {
            msg.ReceivedOnServer(cnn);
        }
        else
        {
            msg.ReceivedOnClient();
        }
    }
    
    //Net Messages(coversation between client and server)
    //When we receive keep_alive message on Client side
    public static Action<NetMessages> C_KEEP_ALIVE;
    public static Action<NetMessages> C_WELCOME;
    public static Action<NetMessages> C_START_GAME;
    public static Action<NetMessages> C_MAKE_MOVE;
    public static Action<NetMessages> C_REMATCH;
    //When we receive keep_alive message on Server side
    public static Action<NetMessages, NetworkConnection> S_KEEP_ALIVE;
    public static Action<NetMessages, NetworkConnection> S_WELCOME;
    public static Action<NetMessages, NetworkConnection> S_START_GAME;
    public static Action<NetMessages, NetworkConnection> S_MAKE_MOVE;
    public static Action<NetMessages, NetworkConnection> S_REMATCH;
}
