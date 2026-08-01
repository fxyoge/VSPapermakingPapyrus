using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.Server;
using Xunit;

namespace PapermakingPapyrus.Tests;

internal static class PapyrusPileTestHelpers
{
    public static bool HasDryingListener(BlockEntityPapyrusPile pile)
    {
        var server = Assert.IsType<ServerMain>(pile.Api.World);
        var field = typeof(Vintagestory.Common.EventManager).GetField(
            "GameTickListenersBlock",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var listeners = Assert.IsAssignableFrom<
            IEnumerable<Vintagestory.Common.GameTickListenerBlock>>(
            field.GetValue(server.EventManager));
        return listeners.Any(listener =>
            listener != null &&
            listener.Pos.Equals(pile.Pos) &&
            ReferenceEquals(listener.HandlerBare?.Target, pile) &&
            listener.HandlerBare.Method.DeclaringType == typeof(BlockEntityPapyrusPile));
    }
}
