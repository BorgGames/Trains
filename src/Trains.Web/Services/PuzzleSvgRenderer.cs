using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Trains.Geometry;
using Trains.Puzzle;
using Trains.Puzzle.Serialization;
using Trains.Track;

namespace Trains.Web.Services;

public sealed class PuzzleSvgRenderer {
    private static readonly string[] Palette = {
        "#e41a1c", "#377eb8", "#4daf4a", "#984ea3", "#ff7f00", "#a65628", "#f781bf", "#999999",
    };

    public string RenderThumbnail(PuzzleSnapshot puzzle) {
        if (puzzle is null)
            throw new ArgumentNullException(nameof(puzzle));

        var state = puzzle.InitialState.ToPuzzleState();
        return Render(puzzle.Track, state, width: 180, height: 140, strokeWidth: 2);
    }

    public string RenderPlayfield(PuzzleSnapshot puzzle, PuzzleState state) {
        if (puzzle is null)
            throw new ArgumentNullException(nameof(puzzle));
        if (state is null)
            throw new ArgumentNullException(nameof(state));

        return Render(puzzle.Track, state, width: 900, height: 650, strokeWidth: 4);
    }

    private static string Render(TrackLayoutSnapshot track, PuzzleState state, int width, int height, int strokeWidth) {
        var points = new List<GridPoint>();
        foreach (var s in track.Segments) {
            points.Add(s.A);
            points.Add(s.B);
        }
        foreach (var t in track.Turntables) {
            points.Add(t.Center);
            foreach (var p in t.Ports)
                points.Add(p.Point);
        }

        if (points.Count == 0)
            points.Add(new GridPoint(0, 0));

        int minX = points.Min(p => p.X);
        int maxX = points.Max(p => p.X);
        int minY = points.Min(p => p.Y);
        int maxY = points.Max(p => p.Y);

        const int cell = 40;
        int pad = cell;

        int viewMinX = (minX * cell) - pad;
        int viewMinY = (-maxY * cell) - pad;
        int viewW = ((maxX - minX) * cell) + 2 * pad;
        int viewH = ((maxY - minY) * cell) + 2 * pad;

        string P(GridPoint p) => string.Create(CultureInfo.InvariantCulture, $"{p.X * cell},{-p.Y * cell}");

        var goalSegments = new HashSet<string>(StringComparer.Ordinal);
        // Goal highlighting is best-effort: goal segment ids come from puzzle definition, but rendering receives only track+state.
        // Callers can overlay goal info separately later; for now, keep it minimal.

        var sb = new StringBuilder();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" ")
          .Append("width=\"").Append(width).Append("\" height=\"").Append(height).Append("\" ")
          .Append("viewBox=\"").Append(viewMinX).Append(' ').Append(viewMinY).Append(' ').Append(viewW).Append(' ').Append(viewH).Append("\">");

        sb.Append("<rect x=\"").Append(viewMinX).Append("\" y=\"").Append(viewMinY).Append("\" width=\"").Append(viewW).Append("\" height=\"").Append(viewH).Append("\" fill=\"white\"/>");

        // Track
        sb.Append("<g stroke=\"#000\" stroke-linecap=\"round\" fill=\"none\">");
        foreach (var s in track.Segments) {
            bool isGoal = goalSegments.Contains(s.Id);
            int sw = isGoal ? strokeWidth + 2 : strokeWidth;

            if (string.Equals(s.Kind, SegmentKinds.Straight, StringComparison.Ordinal)) {
                sb.Append("<line x1=\"").Append(s.A.X * cell).Append("\" y1=\"").Append(-s.A.Y * cell)
                  .Append("\" x2=\"").Append(s.B.X * cell).Append("\" y2=\"").Append(-s.B.Y * cell)
                  .Append("\" stroke-width=\"").Append(sw).Append("\"/>");
            }
            else if (string.Equals(s.Kind, SegmentKinds.Curve, StringComparison.Ordinal)) {
                var a = s.A;
                var b = s.B;
                var bias = s.Bias ?? CurveBias.XFirst;
                var center = bias == CurveBias.XFirst ? new GridPoint(b.X, a.Y) : new GridPoint(a.X, b.Y);

                int r = cell;
                int sweep = ComputeSweep(center, a, b);

                sb.Append("<path d=\"M ").Append(P(a))
                  .Append(" A ").Append(r).Append(' ').Append(r).Append(" 0 0 ").Append(sweep).Append(' ')
                  .Append(P(b))
                  .Append("\" stroke-width=\"").Append(sw).Append("\"/>");
            }
        }
        sb.Append("</g>");

        // Turntables: draw border square and the current alignment bridge.
        foreach (var t in track.Turntables) {
            int cx = t.Center.X * cell;
            int cy = -t.Center.Y * cell;
            int r = t.Radius * cell;

            sb.Append("<rect x=\"").Append(cx - r).Append("\" y=\"").Append(cy - r)
              .Append("\" width=\"").Append(2 * r).Append("\" height=\"").Append(2 * r)
              .Append("\" fill=\"none\" stroke=\"#000\" stroke-width=\"").Append(strokeWidth).Append("\"/>");

            int alignment = 0;
            if (state.TurntableStates.TryGetValue(t.Id, out int idx))
                alignment = idx;
            alignment = Math.Clamp(alignment, 0, Math.Max(0, t.Alignments.Count - 1));

            if (t.Alignments.Count > 0) {
                var a = t.Ports[t.Alignments[alignment].PortAIndex].Point;
                var b = t.Ports[t.Alignments[alignment].PortBIndex].Point;

                sb.Append("<line x1=\"").Append(a.X * cell).Append("\" y1=\"").Append(-a.Y * cell)
                  .Append("\" x2=\"").Append(b.X * cell).Append("\" y2=\"").Append(-b.Y * cell)
                  .Append("\" stroke=\"#000\" stroke-width=\"").Append(strokeWidth).Append("\"/>");
            }
        }

        // Vehicles: render a small rectangle centered on each edge.
        sb.Append("<g stroke=\"#000\" stroke-width=\"1\">");
        foreach (var kvp in state.Placements.OrderBy(k => k.Key)) {
            int vehicleId = kvp.Key;
            var placement = kvp.Value;
            string color = Palette[vehicleId % Palette.Length];

            foreach (var e in placement.Edges) {
                var mid = new GridPoint(e.FromNode.X + e.ToNode.X, e.FromNode.Y + e.ToNode.Y);
                double mx = (mid.X / 2.0) * cell;
                double my = (-mid.Y / 2.0) * cell;

                // Orientation angle from entry heading.
                var (dx, dy) = e.EntryHeading.ToOffset();
                double angle = dx == 0 && dy == 0 ? 0 : Math.Atan2(-dy, dx) * (180.0 / Math.PI);

                sb.Append("<g transform=\"translate(").Append(mx.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(my.ToString(CultureInfo.InvariantCulture)).Append(") rotate(")
                  .Append(angle.ToString(CultureInfo.InvariantCulture)).Append(")\">");

                sb.Append("<rect x=\"-14\" y=\"-8\" width=\"28\" height=\"16\" fill=\"").Append(color).Append("\"/>");
                sb.Append("</g>");
            }
        }
        sb.Append("</g>");

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static int ComputeSweep(GridPoint center, GridPoint start, GridPoint end) {
        // Compute sweep direction via cross product of vectors (center->start) x (center->end) in screen coords.
        double sx = start.X - center.X;
        double sy = -(start.Y - center.Y);
        double ex = end.X - center.X;
        double ey = -(end.Y - center.Y);

        double cross = (sx * ey) - (sy * ex);
        // SVG sweep=1 means "clockwise" in screen coords.
        return cross < 0 ? 1 : 0;
    }
}

