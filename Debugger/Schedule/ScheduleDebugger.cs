﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System.Text;

namespace SinZ.Debugger.Schedule;

internal enum TileDirection
{
    Middle,
    Left,
    Right,
    Up,
    Down,
}
internal record TileInfo(Vector2 coord, TileDirection fromSide, TileDirection toSide);
internal record RenderTileInfo(TileInfo tileInfo, int fromWidth, int fromOffset, int toWidth, int toOffset, List<(Color color, NPC npc, int time, DebugSchedule schedule)> schedules, Color color);
internal record DebugSchedule(List<TileInfo> route, int facingDirection, string endOfRouteBehavior, string endOfRouteMessage, string scheduleName);
public class ScheduleDebugger
{
    private static readonly Lazy<Texture2D> LazyPixel = new(() =>
    {
        Texture2D pixel = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        return pixel;
    });

    private bool IsActive = false;
    private Dictionary<GameLocation, Dictionary<NPC, Dictionary<int, DebugSchedule>>> Schedules = new();
    private Dictionary<GameLocation, Dictionary<Vector2, List<RenderTileInfo>>> RenderTiles = new();

    public ScheduleDebugger(IModHelper helper)
    {
        helper.Events.Display.RenderedWorld += Display_RenderedWorld;
        helper.Events.Display.Rendered += Display_Rendered;
        helper.Events.Input.ButtonPressed += Input_ButtonPressed;
    }

    private void AddLocationWarps(ref Dictionary<(string origin, int x, int y), (string target, int x, int y)> warps, GameLocation gameLocation, NPC npc)
    {
        // TODO: Handle corner cases like BoatTunnel and others
        // TODO: review if location.CollidesWithWarpandDoor will provide better mod support
        foreach (var newwarp in gameLocation.warps)
        {
            warps.TryAdd((gameLocation.NameOrUniqueName, newwarp.X, newwarp.Y), (newwarp.TargetName, newwarp.TargetX, newwarp.TargetY));
            //Monitor.Log($"Location {gameLocation.NameOrUniqueName} has warp ({newwarp.X},{newwarp.Y}) -> ({newwarp.TargetName},{newwarp.TargetX},{newwarp.TargetY})", LogLevel.Debug);
        }
        foreach (var doorPoint in gameLocation.doors.Keys)
        {
            var doorWarp = gameLocation.getWarpFromDoor(doorPoint, npc);
            warps.TryAdd((gameLocation.NameOrUniqueName, doorPoint.X, doorPoint.Y), (doorWarp.TargetName, doorWarp.TargetX, doorWarp.TargetY));
            //Monitor.Log($"Location {gameLocation.NameOrUniqueName} has door ({doorWarp.X},{doorWarp.Y}) -> ({doorWarp.TargetName},{doorWarp.TargetX},{doorWarp.TargetY})", LogLevel.Debug);
        }
        Schedules[gameLocation] = new();
    }

    private IEnumerable<(int offset, int width)> CalculateOffsets(int count)
    {
        var width = Game1.tileSize / 4;
        while (width * count >= Game1.tileSize)
        {
            width -= 2;
        }
        var budget = Game1.tileSize - width * count;
        var buffer = budget / (count + 1);
        var bufferRemainder = budget % (count + 1);

        var outerPad = 0;
        var middlePad = 0;

        if (bufferRemainder % 2 == 1)
        {
            middlePad = 1;
            bufferRemainder -= 1;
        }
        if (bufferRemainder > 0)
        {
            outerPad += bufferRemainder / 2;
        }

        var prevOffset = 0;
        for (var i = 0; i < count; i++)
        {
            int offset;
            if (i == 0)
            {
                offset = outerPad + buffer + width / 2;
            }
            else
            {
                offset = prevOffset + width + buffer;
                if (count % 2 == 0 && i == count / 2)
                {
                    offset += middlePad;
                }
            }
            prevOffset = offset;

            yield return (offset, width);
        }
    }

    private void Input_ButtonPressed(object sender, StardewModdingAPI.Events.ButtonPressedEventArgs e)
    {
        if (ModEntry.Config.ScheduleDebugger.GetState() != SButtonState.Pressed)
        {
            return;
        }
        IsActive = !IsActive;
        if (!IsActive)
        {
            return;
        }
        var discoveredLocations = new List<string>();
        Dictionary<(string origin, int x, int y), (string target, int x, int y)> warps = new();
        Schedules = new();
        // Create a Schedules datastructure grouped by location
        foreach (var location in Game1.locations)
        {
            foreach (var npc in location.characters)
            {
                if (!npc.IsVillager)
                {
                    continue;
                }
                // Temporary
                //if (!npc.Name.ToLower().Contains("kim"))
                //    continue;
                var scheduleName = npc.dayScheduleName.Value;
                var schedule = npc.Schedule;
                if (schedule == null)
                    continue;
                var currentLocation = npc.currentLocation;
                if (!discoveredLocations.Contains(currentLocation.NameOrUniqueName))
                {
                    AddLocationWarps(ref warps, currentLocation, npc);
                    discoveredLocations.Add(currentLocation.NameOrUniqueName);
                }

                foreach ((int time, var scheduleDescription) in schedule)
                {
                    var route = new List<TileInfo>(scheduleDescription.route?.Count ?? 0);
                    TileDirection prevDirection = TileDirection.Middle;
                    if (scheduleDescription.route?.Count > 0)
                    {
                        var points = scheduleDescription.route.ToList();
                        for (var i = 0; i < points.Count; i++)
                        {
                            var point = points[i];
                            (TileDirection from, TileDirection to) shape = (TileDirection.Middle, TileDirection.Middle);
                            // Is this in the 'middle' of the sequence?
                            if (i > 0 && i < points.Count - 1 && prevDirection != TileDirection.Middle)
                            {
                                var prevPoint = points[i - 1];
                                var nextPoint = points[i + 1];
                                int deltaX = nextPoint.X - prevPoint.X;
                                int deltaY = nextPoint.Y - prevPoint.Y;
                                shape = (deltaX, deltaY, prevDirection) switch
                                {
                                    (2, 0, _) => (TileDirection.Left, TileDirection.Right),
                                    (-2, 0, _) => (TileDirection.Right, TileDirection.Left),
                                    (0, 2, _) => (TileDirection.Up, TileDirection.Down),
                                    (0, -2, _) => (TileDirection.Down, TileDirection.Up),
                                    (1, -1, TileDirection.Right) => (TileDirection.Left, TileDirection.Up),
                                    (1, 1, TileDirection.Right) => (TileDirection.Left, TileDirection.Down),
                                    (-1, -1, TileDirection.Left) => (TileDirection.Right, TileDirection.Up),
                                    (-1, 1, TileDirection.Left) => (TileDirection.Right, TileDirection.Down),
                                    (-1, 1, TileDirection.Down) => (TileDirection.Up, TileDirection.Left),
                                    (1, 1, TileDirection.Down) => (TileDirection.Up, TileDirection.Right),
                                    (-1, -1, TileDirection.Up) => (TileDirection.Down, TileDirection.Left),
                                    (1, -1, TileDirection.Up) => (TileDirection.Down, TileDirection.Right),
                                    // probably about to warp
                                    (_, _, TileDirection.Right) => (TileDirection.Left, TileDirection.Middle),
                                    (_, _, TileDirection.Left) => (TileDirection.Right, TileDirection.Middle),
                                    (_, _, TileDirection.Down) => (TileDirection.Up, TileDirection.Middle),
                                    (_, _, TileDirection.Up) => (TileDirection.Down, TileDirection.Middle),
                                    // Catch all
                                    (_, _, _) => (TileDirection.Middle, TileDirection.Middle),
                                };
                            }
                            // Not in the 'middle' of the sequence, is long enough to have a concept of start and end?
                            else if (points.Count > 1)
                            {
                                int deltaX = 0;
                                int deltaY = 0;
                                if (i == 0 || i < points.Count - 1 && prevDirection == TileDirection.Middle)
                                {
                                    // first
                                    var newPoint = points[i + 1];
                                    deltaX = newPoint.X - point.X;
                                    deltaY = newPoint.Y - point.Y;
                                    shape = (deltaX, deltaY) switch
                                    {
                                        (1, 0) => (TileDirection.Middle, TileDirection.Right),
                                        (-1, 0) => (TileDirection.Middle, TileDirection.Left),
                                        (0, 1) => (TileDirection.Middle, TileDirection.Down),
                                        (0, -1) => (TileDirection.Middle, TileDirection.Up),
                                        (_, _) => (TileDirection.Middle, TileDirection.Middle),
                                    };
                                }
                                else
                                {
                                    // last
                                    var oldPoint = points[i - 1];
                                    // intentionally wrong order so its backwards so it aligns with first
                                    deltaX = oldPoint.X - point.X;
                                    deltaY = oldPoint.Y - point.Y;
                                    shape = (deltaX, deltaY) switch
                                    {
                                        (1, 0) => (TileDirection.Right, TileDirection.Middle),
                                        (-1, 0) => (TileDirection.Left, TileDirection.Middle),
                                        (0, 1) => (TileDirection.Down, TileDirection.Middle),
                                        (0, -1) => (TileDirection.Up, TileDirection.Middle),
                                        (_, _) => (TileDirection.Middle, TileDirection.Middle),
                                    };
                                }
                            }
                            // implicit else with default value to be Middle,Middle (ie line went nowhere)
                            route.Add(new TileInfo(point.ToVector2(), shape.from, shape.to));
                            prevDirection = shape.to;
                            // Monitor.Log($"{npc.Name} for {scheduleName}@{time:D4} went to ({point.X},{point.Y})", LogLevel.Debug);
                            if (warps.TryGetValue((currentLocation.NameOrUniqueName, point.X, point.Y), out var warp))
                            {
                                //Monitor.Log($"{npc.Name} for {scheduleName}@{time:D4} warped from {currentLocation.NameOrUniqueName} to {warp.target}@({warp.x},{warp.y})", LogLevel.Info);
                                var newLocation = Game1.getLocationFromName(warp.target);
                                if (!discoveredLocations.Contains(newLocation.NameOrUniqueName))
                                {
                                    AddLocationWarps(ref warps, newLocation, npc);
                                    discoveredLocations.Add(newLocation.NameOrUniqueName);
                                }
                                if (!Schedules[currentLocation].ContainsKey(npc))
                                {
                                    Schedules[currentLocation][npc] = new();
                                }
                                Schedules[currentLocation][npc][time] = new DebugSchedule(route, scheduleDescription.facingDirection, scheduleDescription.endOfRouteBehavior, scheduleDescription.endOfRouteMessage, scheduleName);
                                route = new List<TileInfo>(scheduleDescription.route.Count - route.Count);
                                currentLocation = newLocation;
                            }
                        }
                    }
                    if (!Schedules[currentLocation].ContainsKey(npc))
                    {
                        Schedules[currentLocation][npc] = new();
                    }
                    Schedules[currentLocation][npc][time] = new DebugSchedule(route, scheduleDescription.facingDirection, scheduleDescription.endOfRouteBehavior, scheduleDescription.endOfRouteMessage, scheduleName);
                }
            }
        }
        // Create optimised variant for rendering

        RenderTiles = new Dictionary<GameLocation, Dictionary<Vector2, List<RenderTileInfo>>>();
        foreach ((var location, var npcSchedules) in Schedules)
        {
            RenderTiles[location] = new();
            var uniqueSchedules = new Dictionary<(TileInfo first, TileInfo last), List<(Color color, NPC npc, int time, DebugSchedule schedule)>>();
            var npcIndex = 0;
            foreach ((var npc, var schedules) in npcSchedules)
            {
                var color = Color.Red;
                ColourUtils.IncreaseHueBy(ref color, 360 / npcSchedules.Count * npcIndex++);
                var scheduleColorIndex = 0;
                // TODO: work out a way to not need to do this and interweave this in the previous foreach
                foreach ((var time, var schedule) in schedules)
                {
                    ColourUtils.DecreaseValueBy(ref color, 128 / schedules.Count * scheduleColorIndex++);
                    if (schedule.route == null || schedule.route.Count == 0) continue;
                    var scheduleIndex = (schedule.route[0], schedule.route[^1]);
                    if (!uniqueSchedules.ContainsKey(scheduleIndex))
                    {
                        uniqueSchedules[scheduleIndex] = new List<(Color, NPC, int, DebugSchedule)>();
                    }
                    uniqueSchedules[scheduleIndex].Add((color, npc, time, schedule));
                }
            }
            foreach (var uniqueSchedule in uniqueSchedules.Values)
            {
                var route = uniqueSchedule[0].schedule.route;
                foreach (var point in route)
                {
                    if (!RenderTiles[location].ContainsKey(point.coord))
                    {
                        RenderTiles[location][point.coord] = new();
                    }
                    var fromSchedules = RenderTiles[location][point.coord].Where(row => row.tileInfo.fromSide == point.fromSide || row.tileInfo.toSide == point.fromSide).ToArray();
                    var toSchedules = RenderTiles[location][point.coord].Where(row => row.tileInfo.fromSide == point.toSide || row.tileInfo.toSide == point.toSide).ToArray();
                    var count = RenderTiles[location][point.coord].Count;
                    var renderTile = new RenderTileInfo(
                        tileInfo: point,
                        fromWidth: Game1.tileSize / 2,
                        fromOffset: (int)(Game1.tileSize * 0.5),
                        toWidth: Game1.tileSize / 2,
                        toOffset: (int)(Game1.tileSize * 0.5),
                        schedules: uniqueSchedule,
                        color: uniqueSchedule[0].color
                    );
                    if (fromSchedules.Length > 0)
                    {
                        var offsets = CalculateOffsets(fromSchedules.Length + 1).ToArray();
                        var offsetCount = 0;
                        for (var i = 0; i < count; i++)
                        {
                            var tile = RenderTiles[location][point.coord][i];
                            if (tile.tileInfo.fromSide == point.fromSide)
                            {
                                RenderTiles[location][point.coord][i] = tile with { fromOffset = offsets[offsetCount].offset, fromWidth = offsets[offsetCount].width };
                                if (offsetCount++ == offsets.Length - 2) break;
                            }
                            else if (tile.tileInfo.toSide == point.fromSide)
                            {
                                RenderTiles[location][point.coord][i] = tile with { toOffset = offsets[offsetCount].offset, toWidth = offsets[offsetCount].width };
                                if (offsetCount++ == offsets.Length - 2) break;
                            }
                        }
                        renderTile = renderTile with { fromOffset = offsets[^1].offset, fromWidth = offsets[^1].width };
                    }
                    if (toSchedules.Length > 0)
                    {
                        var offsets = CalculateOffsets(toSchedules.Length + 1).ToArray();
                        var offsetCount = 0;
                        for (var i = 0; i < count; i++)
                        {
                            var tile = RenderTiles[location][point.coord][i];
                            if (tile.tileInfo.fromSide == point.toSide)
                            {
                                RenderTiles[location][point.coord][i] = tile with { fromOffset = offsets[offsetCount].offset, fromWidth = offsets[offsetCount].width };
                                if (offsetCount++ == offsets.Length - 2) break;
                            }
                            else if (tile.tileInfo.toSide == point.toSide)
                            {
                                RenderTiles[location][point.coord][i] = tile with { toOffset = offsets[offsetCount].offset, toWidth = offsets[offsetCount].width };
                                if (offsetCount++ == offsets.Length - 2) break;
                            }
                        }
                        renderTile = renderTile with { toOffset = offsets[^1].offset, toWidth = offsets[^1].width };
                    }
                    RenderTiles[location][point.coord].Add(renderTile);
                }
            }
        }
    }

    private void Display_Rendered(object sender, StardewModdingAPI.Events.RenderedEventArgs e)
    {
        if (!IsActive) return;
        if (Game1.activeClickableMenu != null) return;
        if (Game1.eventUp) return;

        if (RenderTiles.TryGetValue(Game1.player.currentLocation, out var tileInfo))
        {
            if (tileInfo.TryGetValue(Game1.currentCursorTile, out var renderInfo))
            {
                var sb = new StringBuilder();
                sb.AppendLine($"@ {Game1.currentCursorTile.X},{Game1.currentCursorTile.Y} ({Game1.player.currentLocation.Name})");
                foreach (var route in renderInfo)
                {
                    foreach (var schedule in route.schedules)
                    {
                        sb.AppendLine($"{schedule.time:D4}-{schedule.npc.displayName} ({schedule.schedule.scheduleName})");
                    }
                }
                Vector2 tooltipPosition = new Vector2(Game1.getMouseX(), Game1.getMouseY()) + new Vector2(Game1.tileSize / 2f);
                CommonHelper.DrawHoverBox(e.SpriteBatch, sb.ToString(), tooltipPosition, Game1.uiViewport.Width - tooltipPosition.X - Game1.tileSize / 2f);
            }
        }

    }

    private void DrawHorizontal(SpriteBatch spriteBatch, Color color, Vector2 pixel, int offset, int lineWidth, double length = Game1.tileSize, double horizontalOffset = 0)
    {
        spriteBatch.Draw(LazyPixel.Value, new Rectangle(
            x: (int)pixel.X + (int)horizontalOffset,
            y: (int)pixel.Y + offset - lineWidth / 2,
            width: (int)length,
            height: lineWidth),
        color);
    }
    private void DrawVertical(SpriteBatch spriteBatch, Color color, Vector2 pixel, int offset, int lineWidth, double length = Game1.tileSize, double verticalOffset = 0)
    {
        spriteBatch.Draw(LazyPixel.Value, new Rectangle(
            x: (int)pixel.X + offset - lineWidth / 2,
            y: (int)(pixel.Y + verticalOffset),
            width: lineWidth,
            height: (int)length),
        color);
    }
    private void DrawHorizontalBridge(SpriteBatch spriteBatch, Color color, Vector2 pixel, RenderTileInfo tile)
    {
        var rightWidth = tile.fromOffset > tile.toOffset ? tile.fromWidth : tile.toWidth;
        var rightOffset = tile.fromOffset > tile.toOffset ? tile.fromOffset : tile.toOffset;
        var leftWidth = tile.fromOffset < tile.toOffset ? tile.fromWidth : tile.toWidth;
        var leftOffset = tile.fromOffset < tile.toOffset ? tile.fromOffset : tile.toOffset;
        DrawHorizontal(spriteBatch, color, pixel, Game1.tileSize / 2, Math.Min(tile.fromWidth, tile.toWidth),
            length: rightOffset + rightWidth / 2 - (leftOffset - leftWidth / 2),
            horizontalOffset: leftOffset - leftWidth / 2
        );
    }
    private void DrawVerticalBridge(SpriteBatch spriteBatch, Color color, Vector2 pixel, RenderTileInfo tile)
    {
        var downWidth = tile.fromOffset > tile.toOffset ? tile.fromWidth : tile.toWidth;
        var downOffset = tile.fromOffset > tile.toOffset ? tile.fromOffset : tile.toOffset;
        var upWidth = tile.fromOffset < tile.toOffset ? tile.fromWidth : tile.toWidth;
        var upOffset = tile.fromOffset < tile.toOffset ? tile.fromOffset : tile.toOffset;
        DrawVertical(spriteBatch, color, pixel, Game1.tileSize / 2, Math.Min(tile.fromWidth, tile.toWidth),
            length: downOffset + downWidth / 2 - (upOffset - upWidth / 2),
            verticalOffset: upOffset - upWidth / 2
        );
    }

    private void Display_RenderedWorld(object sender, StardewModdingAPI.Events.RenderedWorldEventArgs e)
    {
        if (!IsActive) return;
        if (RenderTiles.TryGetValue(Game1.player.currentLocation, out var tileInfo))
        {
            var visibileTiles = TileHelper.GetVisibleTiles(expand: 1);
            foreach (var tile in visibileTiles)
            {
                if (tileInfo.TryGetValue(tile, out var renderInfo))
                {
                    foreach (var renderTile in renderInfo)
                    {
                        var point = renderTile.tileInfo;
                        var color = renderTile.color;
                        Vector2 pixel = point.coord * Game1.tileSize - new Vector2(Game1.viewport.X, Game1.viewport.Y);
                        switch ((point.fromSide, point.toSide))
                        {
                            case (TileDirection.Right, TileDirection.Middle):
                                DrawHorizontal(e.SpriteBatch, color, pixel, renderTile.fromOffset, renderTile.fromWidth, Game1.tileSize / 2, Game1.tileSize / 2);
                                break;
                            case (TileDirection.Middle, TileDirection.Right):
                                DrawHorizontal(e.SpriteBatch, color, pixel, renderTile.toOffset, renderTile.toWidth, Game1.tileSize / 2, Game1.tileSize / 2);
                                break;
                            case (TileDirection.Left, TileDirection.Middle):
                                DrawHorizontal(e.SpriteBatch, color, pixel, renderTile.fromOffset, renderTile.fromWidth, Game1.tileSize / 2);
                                break;
                            case (TileDirection.Middle, TileDirection.Left):
                                DrawHorizontal(e.SpriteBatch, color, pixel, renderTile.toOffset, renderTile.toWidth, Game1.tileSize / 2);
                                break;
                            case (TileDirection.Down, TileDirection.Middle):
                                DrawVertical(e.SpriteBatch, color, pixel, renderTile.fromOffset, renderTile.fromWidth, Game1.tileSize / 2, Game1.tileSize / 2);
                                break;
                            case (TileDirection.Middle, TileDirection.Down):
                                DrawVertical(e.SpriteBatch, color, pixel, renderTile.toOffset, renderTile.toWidth, Game1.tileSize / 2, Game1.tileSize / 2);
                                break;
                            case (TileDirection.Up, TileDirection.Middle):
                                DrawVertical(e.SpriteBatch, color, pixel, renderTile.fromOffset, renderTile.fromWidth, Game1.tileSize / 2);
                                break;
                            case (TileDirection.Middle, TileDirection.Up):
                                DrawVertical(e.SpriteBatch, color, pixel, renderTile.toOffset, renderTile.toWidth, Game1.tileSize / 2);
                                break;
                            case (TileDirection.Left, TileDirection.Right):
                                DrawHorizontal(e.SpriteBatch, color, pixel, renderTile.fromOffset, renderTile.fromWidth, Game1.tileSize / 2);
                                DrawVerticalBridge(e.SpriteBatch, color, pixel, renderTile);
                                DrawHorizontal(e.SpriteBatch, color, pixel, renderTile.toOffset, renderTile.toWidth, Game1.tileSize / 2, Game1.tileSize / 2);
                                break;
                            case (TileDirection.Right, TileDirection.Left):
                                DrawHorizontal(e.SpriteBatch, color, pixel, renderTile.toOffset, renderTile.toWidth, Game1.tileSize / 2);
                                DrawVerticalBridge(e.SpriteBatch, color, pixel, renderTile);
                                DrawHorizontal(e.SpriteBatch, color, pixel, renderTile.fromOffset, renderTile.fromWidth, Game1.tileSize / 2, Game1.tileSize / 2);
                                break;
                            case (TileDirection.Up, TileDirection.Down):
                                DrawVertical(e.SpriteBatch, color, pixel, renderTile.fromOffset, renderTile.fromWidth, Game1.tileSize / 2);
                                DrawHorizontalBridge(e.SpriteBatch, color, pixel, renderTile);
                                DrawVertical(e.SpriteBatch, color, pixel, renderTile.toOffset, renderTile.toWidth, Game1.tileSize / 2, Game1.tileSize / 2);
                                break;
                            case (TileDirection.Down, TileDirection.Up):
                                DrawVertical(e.SpriteBatch, color, pixel, renderTile.toOffset, renderTile.toWidth, Game1.tileSize / 2);
                                DrawHorizontalBridge(e.SpriteBatch, color, pixel, renderTile);
                                DrawVertical(e.SpriteBatch, color, pixel, renderTile.fromOffset, renderTile.fromWidth, Game1.tileSize / 2, Game1.tileSize / 2);
                                break;
                            case (TileDirection.Up, TileDirection.Right):
                                DrawVertical(e.SpriteBatch, color, pixel, renderTile.fromOffset, renderTile.fromWidth,
                                    length: renderTile.toOffset + renderTile.toWidth / 2
                                );
                                DrawHorizontal(e.SpriteBatch, color, pixel, renderTile.toOffset, renderTile.toWidth,
                                    length: Game1.tileSize - (renderTile.fromOffset + renderTile.fromWidth / 2),
                                    horizontalOffset: renderTile.fromOffset + renderTile.fromWidth / 2
                                );
                                break;
                            case (TileDirection.Right, TileDirection.Up):
                                DrawHorizontal(e.SpriteBatch, color, pixel, renderTile.fromOffset, renderTile.fromWidth,
                                    length: Game1.tileSize - (renderTile.toOffset + renderTile.toWidth / 2),
                                    horizontalOffset: renderTile.toOffset + renderTile.toWidth / 2
                                );
                                DrawVertical(e.SpriteBatch, color, pixel, renderTile.toOffset, renderTile.toWidth,
                                    length: renderTile.fromOffset + renderTile.fromWidth / 2
                                );
                                break;
                            case (TileDirection.Down, TileDirection.Left):
                                DrawVertical(e.SpriteBatch, color, pixel, renderTile.fromOffset, renderTile.fromWidth,
                                    length: Game1.tileSize - (renderTile.toOffset + renderTile.toWidth / 2),
                                    verticalOffset: renderTile.toOffset + renderTile.toWidth / 2
                                );
                                DrawHorizontal(e.SpriteBatch, color, pixel, renderTile.toOffset, renderTile.toWidth,
                                    length: renderTile.fromOffset + renderTile.fromWidth / 2
                                );
                                break;
                            case (TileDirection.Left, TileDirection.Down):
                                DrawHorizontal(e.SpriteBatch, color, pixel, renderTile.fromOffset, renderTile.fromWidth,
                                    length: renderTile.toOffset + renderTile.toWidth / 2
                                );
                                DrawVertical(e.SpriteBatch, color, pixel, renderTile.toOffset, renderTile.toWidth,
                                    length: Game1.tileSize - (renderTile.fromOffset + renderTile.fromWidth / 2),
                                    verticalOffset: renderTile.fromOffset + renderTile.fromWidth / 2
                                );
                                break;
                            case (TileDirection.Down, TileDirection.Right):
                                DrawVertical(e.SpriteBatch, color, pixel, renderTile.fromOffset, renderTile.fromWidth,
                                    length: Game1.tileSize - (renderTile.toOffset + renderTile.toWidth / 2),
                                    verticalOffset: renderTile.toOffset + renderTile.toWidth / 2
                                );
                                DrawHorizontal(e.SpriteBatch, color, pixel, renderTile.toOffset, renderTile.toWidth,
                                    length: Game1.tileSize - (renderTile.fromOffset - renderTile.fromWidth / 2),
                                    horizontalOffset: renderTile.fromOffset - renderTile.fromWidth / 2
                                );
                                break;
                            case (TileDirection.Right, TileDirection.Down):
                                DrawHorizontal(e.SpriteBatch, color, pixel, renderTile.fromOffset, renderTile.fromWidth,
                                    length: Game1.tileSize - (renderTile.toOffset - renderTile.toWidth / 2),
                                    horizontalOffset: renderTile.toOffset - renderTile.toWidth / 2
                                );
                                DrawVertical(e.SpriteBatch, color, pixel, renderTile.toOffset, renderTile.toWidth,
                                    length: Game1.tileSize - (renderTile.fromOffset + renderTile.fromWidth / 2),
                                    verticalOffset: renderTile.fromOffset + renderTile.fromWidth / 2
                                );
                                break;
                            case (TileDirection.Up, TileDirection.Left):
                                DrawVertical(e.SpriteBatch, color, pixel, renderTile.fromOffset, renderTile.fromWidth,
                                    length: renderTile.toOffset + renderTile.toWidth / 2
                                );
                                DrawHorizontal(e.SpriteBatch, color, pixel, renderTile.toOffset, renderTile.toWidth,
                                    length: renderTile.fromOffset + renderTile.fromWidth / 2
                                );
                                break;
                            case (TileDirection.Left, TileDirection.Up):
                                DrawHorizontal(e.SpriteBatch, color, pixel, renderTile.fromOffset, renderTile.fromWidth,
                                    length: renderTile.toOffset + renderTile.toWidth / 2
                                );
                                DrawVertical(e.SpriteBatch, color, pixel, renderTile.toOffset, renderTile.toWidth,
                                    length: renderTile.fromOffset + renderTile.fromWidth / 2
                                );
                                break;

                            case (_, _):
                                e.SpriteBatch.Draw(LazyPixel.Value, new Rectangle(
                                    x: (int)pixel.X + renderTile.fromOffset - renderTile.fromWidth / 2,
                                    y: (int)pixel.Y + renderTile.fromOffset - renderTile.fromWidth / 2,
                                    width: renderTile.fromWidth,
                                    height: renderTile.fromWidth),
                                color);
                                break;
                        }
                    }
                }
            }
        }
    }

    internal void PopulateConfig(IGenericModConfigMenuApi configMenu, IManifest modManifest)
    {
        configMenu.AddSectionTitle(modManifest, () => "Schedule Debugger");
        configMenu.AddParagraph(modManifest, () => "The Schedule Debugger when activated will grab the schedule state of all NPC's once, and then visualize it until turned off.");
        configMenu.AddKeybindList(modManifest,
            getValue: () => ModEntry.Config.ScheduleDebugger,
            setValue: v => ModEntry.Config.ScheduleDebugger = v,
            name: () => "Toggle Schedule Debugger");
    }
}
