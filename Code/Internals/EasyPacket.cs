/*
 *  EasyPacket.cs
 *  DavidFDev
 */

using System.IO;
using Terraria;
using Terraria.ID;

namespace EasyPacketsLib.Internals;

/// <summary>
///     Used to receive an incoming packet and detour it to the struct.
/// </summary>
internal static class EasyPacket
{
    #region Methods

    public static void ReceivePacket(in IEasyPacket packet, BinaryReader reader, in SenderInfo sender)
    {
        var prev = reader.BaseStream.Position;

        packet.Deserialise(reader, in sender);

        // Check if the packet should be automatically forwarded to clients
        if (Main.netMode == NetmodeID.Server && sender.Forwarded)
        {
            EasyPacketExtensions.SendPacket_Internal(sender.Mod, in packet, sender.WhoAmI, sender.ToClient, sender.IgnoreClient, true);
        }

        // Handle the received packet
        var handled = false;
        packet.Receive(in sender, ref handled);

        if (!handled)
        {
            sender.Mod.Logger.Error($"Unhandled packet: {packet.GetType().Name}.");
        }

        sender.Mod.Logger.Info($"Read {reader.BaseStream.Position - prev} bytes for packet of type {packet.GetType().Name}." +
            $"{reader.BaseStream.Length - reader.BaseStream.Position} left");
    }

    #endregion
}