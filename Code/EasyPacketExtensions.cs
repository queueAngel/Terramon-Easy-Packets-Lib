/*
 *  EasyPacketExtensions.cs
 *  DavidFDev
 */

using EasyPacketsLib.Internals;
using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace EasyPacketsLib;

/// <summary>
///     Extension methods for sending easy packets and handling how they are received.
/// </summary>
public static class EasyPacketExtensions
{
    #region Static Methods

    /// <summary>
    ///     Send an easy packet.
    ///     If a packet is received but is unhandled, an error is raised.
    /// </summary>
    /// <example>
    ///     <code>Mod.SendPacket(new ExamplePacket(10, 20));</code>
    /// </example>
    /// <param name="mod">Mod sending the packet.</param>
    /// <param name="packet">Packet instance that implements <see cref="IEasyPacket" />.</param>
    /// <param name="toClient">If non-negative, this packet will only be sent to the specified client.</param>
    /// <param name="ignoreClient">If non-negative, this packet will not be sent to the specified client.</param>
    /// <param name="forward">If sending from a client, this packet will be forwarded to other clients through the server.</param>
    public static void SendPacket(this Mod mod, in IEasyPacket packet, int toClient = -1, int ignoreClient = -1, bool forward = false)
    {
        forward = forward && Main.netMode == NetmodeID.MultiplayerClient;
        SendPacket_Internal(mod, in packet, (byte)Main.myPlayer, toClient, ignoreClient, forward);
    }

    /// <summary>
    ///     Serialise an easy packet.
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="packet">Packet instance that implements <see cref="IEasyPacket" />.</param>
    public static void Write(this BinaryWriter writer, in IEasyPacket packet)
    {
        packet.Serialise(writer);
    }

    /// <summary>
    ///     Deserialise an easy packet.
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="sender">Information regarding the sender of the packet.</param>
    /// <typeparam name="T">Type that implements <see cref="IEasyPacket" />.</typeparam>
    /// <returns>Packet instance that implements <see cref="IEasyPacket" />.</returns>
    public static T Read<T>(this BinaryReader reader, in SenderInfo sender) where T : struct, IEasyPacket
    {
        var packet = new T();
        packet.Deserialise(reader, in sender);
        return packet;
    }

    internal static void SendPacket_Internal(Mod mod, in IEasyPacket packet, byte whoAmI, int toClient, int ignoreClient, bool forward)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            throw new Exception("SendPacket called in single-player.");
        }

        if (!EasyPacketLoader.IsRegistered(in packet))
        {
            throw new Exception($"SendPacket called on an unregistered type: {packet.GetType().Name}.");
        }

        // Check if the mod is synced
        if (!mod.IsNetSynced)
        {
            // Client's IsNetSynced is true if Side=Both, but if Side=NoSync, true if the server has the mod
            // Server's IsNetSynced is true if Side=Both or Side=NoSync
            if (Main.netMode == NetmodeID.MultiplayerClient && mod.Side == ModSide.NoSync)
            {
                return;
            }

            throw new Exception("SendPacket called on an un-synced mod.");
        }

        // Important that the packet is sent by this mod, so that it is received correctly
        var modPacket = mod.GetPacket();

        // Mod's net id is synced across server and clients
        var modNetId = mod.NetID;
        if (ModNet.NetModCount < 256)
        {
            modPacket.Write((byte)modNetId);
        }
        else
        {
            modPacket.Write(modNetId);
        }

        // Easy packet type's net id is synced across server and clients
        var packetNetId = EasyPacketLoader.GetNetId(in packet);
        if (EasyPacketLoader.NetEasyPacketCount < 256)
        {
            modPacket.Write((byte)packetNetId);
        }
        else
        {
            modPacket.Write(packetNetId);
        }

        // Write any additional flags
        var expected = mod.Side == ModSide.Both;
        var flags = new BitsByte { [0] = forward, [1] = expected };
        modPacket.Write(flags);

        // Special case if the packet is to be forwarded
        if (forward)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // Send this so that the server knows who to forward the packet to
                modPacket.Write(toClient < 0 ? (byte)255 : (byte)toClient);
                modPacket.Write(ignoreClient < 0 ? (byte)255 : (byte)ignoreClient);
            }
            else
            {
                // Send this so that the receiving client knows who originally forwarded the packet
                modPacket.Write(whoAmI);
            }
        }

        // Let the easy packet serialise itself
        packet.Serialise(modPacket);

        // Finally, send the packet
        modPacket.Send(toClient, ignoreClient);
    }

    internal static void HandlePacket_Internal(BinaryReader reader, int whoAmI)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            throw new Exception("HandlePacket called in single-player.");
        }

        whoAmI = Math.Clamp(whoAmI, 0, 255);

        var modNetId = ModNet.NetModCount < 256 ? reader.ReadByte() : reader.ReadInt16();
        var packetNetId = EasyPacketLoader.NetEasyPacketCount < 256 ? reader.ReadByte() : reader.ReadUInt16();
        var flags = (BitsByte)reader.ReadByte();
        var forward = flags[0];
        var expected = flags[1];

        // Get the mod that sent the packet using its net id
        var sentByMod = ModNet.GetMod(modNetId);

        // Check if the mod exists and is synced
        if (sentByMod is not { IsNetSynced: true })
        {
            // Don't throw if it's okay that the mod doesn't exist
            // This means the mod on the server has Side=NoSync and this client doesn't have the mod
            if (Main.netMode == NetmodeID.MultiplayerClient && !expected)
            {
                return;
            }

            throw new Exception($"HandlePacket received an invalid mod Net ID: {modNetId}. Could not find a mod with that Net ID.");
        }

        // Get the easy packet mod type using its net id
        var packet = EasyPacketLoader.GetPacket(packetNetId) ??
            throw new Exception($"HandlePacket received an invalid easy mod packet with Net ID: {packetNetId}. Could not find an easy mod packet with that Net ID.");

        // DEBUG: Store the type of the currently handled packet
#if DEBUG
        EasyPacket.lastProcessedPacket = packet.GetType();
#endif

        // Special case if the packet was forwarded
        byte toClient = 255;
        byte ignoreClient = 255;
        if (forward)
        {
            if (Main.netMode == NetmodeID.Server)
            {
                // Server knows who to forward the packet to
                toClient = reader.ReadByte();
                ignoreClient = reader.ReadByte();
            }
            else
            {
                // Client knows who originally forwarded the packet
                whoAmI = reader.ReadByte();
            }
        }

        // Let the easy packet mod type receive the packet
        EasyPacket.ReceivePacket(in packet, reader, new SenderInfo(sentByMod, (byte)whoAmI, flags, toClient, ignoreClient));
    }

    #endregion
}