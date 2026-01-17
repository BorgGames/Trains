using Trains.Engine;
using Trains.Geometry;
using Trains.Puzzle;
using Trains.Puzzle.Serialization;
using Trains.Track;
using Xunit;

namespace Trains.Core.Tests;

public sealed class PuzzleStateJsonTests {
    [Fact]
    public void Roundtrip_PreservesPlacementsCouplingsAndTurntables() {
        var seg = new StraightSegment("S0", new GridPoint(0, 0), new GridPoint(1, 0));
        var track = TrackLayout.Create(new[] { seg });

        var car0 = new CarSpec(id: 0, length: 1, weight: 1);
        var engine1 = new EngineSpec(id: 1, length: 1, weight: 0, forwardPower: 1, backwardPower: 1);

        var state = new PuzzleState();
        state.TurntableStates["T0"] = 1;
        state.Placements.Add(0, new VehiclePlacement(0, new[] { seg.GetDirectedEdges()[0] }));
        state.Placements.Add(1, new VehiclePlacement(1, new[] { seg.GetDirectedEdges()[1] }));
        state.Couplings.Add(0, new VehicleCouplings { Front = new VehicleCoupling(1, VehicleEnd.Back) });
        state.Couplings.Add(1, new VehicleCouplings { Back = new VehicleCoupling(0, VehicleEnd.Front) });
        state.SwitchStates[new TrackState(new GridPoint(0, 0), Direction.East)] = 0;

        var snapshot = PuzzleStateSnapshot.FromPuzzleState(state);
        var json = PuzzleStateJson.Serialize(snapshot);
        var round = PuzzleStateJson.Deserialize(json);
        var state2 = round.ToPuzzleState();

        Assert.Equal(state.SwitchStates.Count, state2.SwitchStates.Count);
        Assert.Equal(state.TurntableStates["T0"], state2.TurntableStates["T0"]);
        Assert.Equal(state.Placements.Count, state2.Placements.Count);
        Assert.Equal(state.Couplings.Count, state2.Couplings.Count);
        Assert.Equal(state.Placements[0].Edges[0], state2.Placements[0].Edges[0]);
    }
}

