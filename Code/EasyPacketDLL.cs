/*
 *  EasyPacketDLL.cs
 *  DavidFDev
 */

using EasyPacketsLib.Internals;
using System.IO;
using Terraria.ModLoader;

namespace EasyPacketsLib;

/// <summary>
///     Methods for using the library as a DLL reference instead of as a mod reference.
/// </summary>
public static class EasyPacketDLL
{
    #region Static Methods

    /// <summary>
    ///     Register easy packets and handlers of the provided mod.
    ///     If registering multiple mods, ensure the order is the same across clients.
    ///     <example>
    ///         <code>public override void Load() => EasyPacketDLL.RegisterMod(this);</code>
    ///     </example>
    /// </summary>
    /// <param name="mod">Mod to load the types from.</param>
    public static void RegisterMod(Mod mod)
    {
        EasyPacketLoader.RegisterMod(mod);
    }

    /// <summary>
    ///     Clear static references when the mod is unloaded.
    ///     It is recommended to call this so that references are properly cleared.
    ///     <example>
    ///         <code>public override void Unload() => EasyPacketDLL.Unload();</code>
    ///     </example>
    /// </summary>
    public static void Unload()
    {
        EasyPacketLoader.ClearStatics();
    }

    /// <summary>
    ///     Handle incoming easy packets.
    ///     <example>
    ///         <code>public override void HandlePacket(BinaryReader reader, int whoAmI) => EasyPacketDLL.HandlePacket(reader, whoAmI);</code>
    ///     </example>
    /// </summary>
    public static void HandlePacket(BinaryReader reader, int whoAmI)
    {
        EasyPacketExtensions.HandlePacket_Internal(reader, whoAmI);
    }

    #endregion
}