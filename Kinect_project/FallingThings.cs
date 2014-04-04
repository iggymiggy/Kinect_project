//------------------------------------------------------------------------------
// <copyright file="FallingThings.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

// This module contains code to do display falling shapes, and do
// hit testing against a set of segments provided by the Kinect NUI, and
// have shapes react accordingly.

namespace ShapeGame
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Shapes;
    using Microsoft.Kinect;
    using ShapeGame.Utils;
    using System.Windows.Media.Imaging;

    // FallingThings is the main class to draw and maintain positions of falling shapes.  It also does hit testing
    // and appropriate bouncing.
    public class FallingThings
    {
        private const double BaseGravity = 0.017;
        private const double XVelocity = 0.5;
        private const double BaseAirFriction = 0.994;

        private readonly Dictionary<PolyType, PolyDef> polyDefs = new Dictionary<PolyType, PolyDef>
            {
                { PolyType.Square, new PolyDef { Sides = 4, Skip = 1 } }//,
            };

        private readonly List<Thing> things = new List<Thing>();
        private readonly Random rnd = new Random();
        private readonly int maxThings;
        private readonly int intraFrames = 1;
        private readonly Dictionary<int, int> scores = new Dictionary<int, int>();
        private const double DissolveTime = 0.4;
        private Rect sceneRect;
        private double targetFrameRate = 60;
        private double dropRate = 2.0;
        private double shapeSize = 1.0;
        private double baseShapeSize = 20;
        private GameMode gameMode = GameMode.Off;
        private double gravity = BaseGravity;
        private double gravityFactor = 1.0;
        private double xvelocity = XVelocity;
        private double airFriction = BaseAirFriction;
        private int frameCount;
        private bool doRandomColors = true;
        private double expandingRate = 1.0;
        private System.Windows.Media.Color baseColor = System.Windows.Media.Color.FromRgb(0, 0, 0);
        private PolyType polyTypes = PolyType.All;
        private DateTime gameStartTime;
        private BitmapImage orc = new BitmapImage(new Uri("Resources/sumo.png", UriKind.Relative));
        private BitmapImage earth = new BitmapImage(new Uri("Resources/earth.gif", UriKind.Relative));
        private BitmapImage tim = new BitmapImage(new Uri("Resources/Tim.png", UriKind.Relative));
        private BitmapImage tim0 = new BitmapImage(new Uri("Resources/0.png", UriKind.Relative));
        private List<BitmapImage> timlist = new List<BitmapImage>();
        private List<BitmapImage> ninjalist = new List<BitmapImage>();
        private List<BitmapImage> karatelist = new List<BitmapImage>();
        private List<BitmapImage> krokolist = new List<BitmapImage>();
        private List<BitmapImage> blacklist = new List<BitmapImage>();
        //create arrays for sprites
        public void list_loader(int type)
        {
            if (type == 0)
            {
                for (int i = 0; i <= 26; i++)
                {
                    string name = i.ToString(CultureInfo.InvariantCulture);
                    name = "Resources/" + name + ".png";
                    this.timlist.Add(new BitmapImage(new Uri(name, UriKind.Relative)));

                }
            }
            if (type == 1)
            {

                for (int i = 1; i <= 14; i++)
                {
                    string name = i.ToString(CultureInfo.InvariantCulture);
                    name = "Resources/ninja (" + name + ").png";
                    this.ninjalist.Add(new BitmapImage(new Uri(name, UriKind.Relative)));

                }
            }
            if (type == 2)
            {

                for (int i = 1; i <= 82; i++)
                {
                    string name = i.ToString(CultureInfo.InvariantCulture);
                    name = "Resources/karate (" + name + ").png";
                    this.karatelist.Add(new BitmapImage(new Uri(name, UriKind.Relative)));

                }
            }
            if (type == 3)
            {

                for (int i = 1; i <= 17; i++)
                {
                    string name = i.ToString(CultureInfo.InvariantCulture);
                    name = "Resources/kroko (" + name + ").png";
                    this.krokolist.Add(new BitmapImage(new Uri(name, UriKind.Relative)));

                }
            }
            if (type == 4)
            {

                for (int i = 1; i <= 22; i++)
                {
                    string name = i.ToString(CultureInfo.InvariantCulture);
                    name = "Resources/black (" + name + ").png";
                    this.blacklist.Add(new BitmapImage(new Uri(name, UriKind.Relative)));

                }
            }
 
        }
    
        public int bonus = 0;
        public int max_bonus = 30;
        public int missed_blocks = 0;
        public int max_missed = 100;
        public int stopped = 0;

        public FallingThings(int maxThings, double framerate, int intraFrames)
        {
            this.maxThings = maxThings;
            this.intraFrames = intraFrames;
            this.targetFrameRate = framerate * intraFrames;
            this.SetGravity(this.gravityFactor);
            this.sceneRect.X = this.sceneRect.Y = 0;
            this.sceneRect.Width = this.sceneRect.Height = 100;
            this.shapeSize = this.sceneRect.Height * this.baseShapeSize / 1000.0;
            this.expandingRate = Math.Exp(Math.Log(6.0) / (this.targetFrameRate * DissolveTime));
        }

        public enum ThingState
        {
            Falling = 0,
            Bouncing = 1,
            Dissolving = 2,
            Remove = 3
        }
        //sprite type
        public enum enemy_type
        {
            Orc = 0,
            basic = 1,
            ninja = 2,
            karate = 3,
            kroko = 4,
            black = 5
        }

        public static Label MakeSimpleLabel(string text, Rect bounds, System.Windows.Media.Brush brush)
        {
            Label label = new Label { Content = text };
            if (bounds.Width != 0)
            {
                label.SetValue(Canvas.LeftProperty, bounds.Left);
                label.SetValue(Canvas.TopProperty, bounds.Top);
                label.Width = bounds.Width;
                label.Height = bounds.Height;
            }

            label.Foreground = brush;
            label.FontFamily = new System.Windows.Media.FontFamily("Arial");
            label.FontWeight = FontWeight.FromOpenTypeWeight(600);
            label.FontStyle = FontStyles.Normal;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            return label;
        }

        public void ResetScores()
        {
            
        }

        public void SetFramerate(double actualFramerate)
        {
            this.targetFrameRate = actualFramerate * this.intraFrames;
            this.expandingRate = Math.Exp(Math.Log(6.0) / (this.targetFrameRate * DissolveTime));
            if (this.gravityFactor != 0)
            {
                this.SetGravity(this.gravityFactor);
            }
        }

        public void SetBoundaries(Rect r)
        {
            this.sceneRect = r;
            this.shapeSize = r.Height * this.baseShapeSize / 1000.0;
        }

        public void SetDropRate(double f)
        {
            this.dropRate = f;
        }
        public void SetXvelocity(double f)
        {
            this.xvelocity = f;
        }

        public void SetSize(double f)
        {
            this.baseShapeSize = f;
            this.shapeSize = this.sceneRect.Height * this.baseShapeSize / 1000.0;
        }

        public void SetShapesColor(System.Windows.Media.Color color, bool doRandom)
        {
            this.doRandomColors = doRandom;
            this.baseColor = color;
        }

        public void Reset()
        {
            for (int i = 0; i < this.things.Count; i++)
            {
                Thing thing = this.things[i];
                if ((thing.State == ThingState.Bouncing) || (thing.State == ThingState.Falling))
                {
                    thing.State = ThingState.Dissolving;
                    thing.Dissolve = 0;
                    this.things[i] = thing;
                    this.missed_blocks = 0;
                }
            }

            this.gameStartTime = DateTime.Now;
            this.scores.Clear();
            this.missed_blocks= 0;
        }

        public void SetGameMode(GameMode mode)
        {
            this.gameMode = mode;
            this.gameStartTime = DateTime.Now;
            this.scores.Clear();
        }

        public void SetGravity(double f)
        {
            this.gravityFactor = f;
            this.gravity = f * BaseGravity / this.targetFrameRate / Math.Sqrt(this.targetFrameRate) / Math.Sqrt(this.intraFrames);
            this.airFriction = f == 0 ? 0.997 : Math.Exp(Math.Log(1.0 - ((1.0 - BaseAirFriction) / f)) / this.intraFrames);

            if (f == 0)
            {
                // Stop all movement as well!
                for (int i = 0; i < this.things.Count; i++)
                {
                    Thing thing = this.things[i];
                    thing.XVelocity = thing.YVelocity = 0;
                    this.things[i] = thing;
                }
            }
        }

        public void SetPolies(PolyType polies)
        {
            this.polyTypes = polies;
        }

        //HITS
        public HitType LookForHits(Dictionary<Bone, BoneData> segments, int playerId)
        {
            
            DateTime cur = DateTime.Now;
            HitType allHits = HitType.None;
            if (this.stopped == 1)
            {
                return allHits;
            }
            // Zero out score if necessary
            if (!this.scores.ContainsKey(playerId))
            {
                this.scores.Add(playerId, 0);
            }

            foreach (var pair in segments)
            {
                for (int i = 0; i < this.things.Count; i++)
                {
                    HitType hit = HitType.None;
                    Thing thing = this.things[i];
                    switch (thing.State)
                    {
                        case ThingState.Bouncing:
                        case ThingState.Falling:
                            {
                                var hitCenter = new System.Windows.Point(0, 0);
                                double lineHitLocation = 0;
                                Segment seg = pair.Value.GetEstimatedSegment(cur);
                                if (thing.Hit(seg, ref hitCenter, ref lineHitLocation))
                                {
                                    double fMs = 1000;
                                    if (thing.TimeLastHit != DateTime.MinValue)
                                    {
                                        fMs = cur.Subtract(thing.TimeLastHit).TotalMilliseconds;
                                        thing.AvgTimeBetweenHits = (thing.AvgTimeBetweenHits * 0.8) + (0.2 * fMs);
                                    }

                                    thing.TimeLastHit = cur;

                                    // Bounce off head and hands
                                    if (seg.IsCircle())
                                    {
                                        // Bounce off of hand/head/foot
                                        thing.BounceOff(
                                            hitCenter.X,
                                            hitCenter.Y,
                                            seg.Radius,
                                            pair.Value.XVelocity / this.targetFrameRate,
                                            pair.Value.YVelocity / this.targetFrameRate);

                                        if (fMs > 100.0)
                                        {
                                            hit |= HitType.Hand;
                                        }
                                    }
                                    else
                                    {
                                        // Bounce off line segment
                                        double velocityX = (pair.Value.XVelocity * (1.0 - lineHitLocation)) + (pair.Value.XVelocity2 * lineHitLocation);
                                        double velocityY = (pair.Value.YVelocity * (1.0 - lineHitLocation)) + (pair.Value.YVelocity2 * lineHitLocation);

                                        thing.BounceOff(
                                            hitCenter.X,
                                            hitCenter.Y,
                                            seg.Radius,
                                            velocityX / this.targetFrameRate,
                                            velocityY / this.targetFrameRate);

                                        if (fMs > 100.0)
                                        {
                                            hit |= HitType.Arm;
                                        }
                                    }

                                    if (this.gameMode == GameMode.TwoPlayer)
                                    {
                                        if (thing.State == ThingState.Falling)
                                        {
                                            thing.State = ThingState.Bouncing;
                                            thing.TouchedBy = playerId;
                                            thing.Hotness = 1;
                                            thing.FlashCount = 0;
                                        }
                                        else if (thing.State == ThingState.Bouncing)
                                        {
                                            if (thing.TouchedBy != playerId)
                                            {
                                                if (seg.IsCircle())
                                                {
                                                    thing.TouchedBy = playerId;
                                                    thing.Hotness = Math.Min(thing.Hotness + 1, 4);
                                                }
                                                else
                                                {
                                                    hit |= HitType.Popped;
                                                    this.AddToScore(thing.TouchedBy, 5 << (thing.Hotness - 1), thing.Center);
                                                }
                                            }
                                        }
                                    }
                                    else if (this.gameMode == GameMode.Solo)
                                    {
                                        if (seg.IsCircle())
                                        {
                                            if (thing.State == ThingState.Falling)
                                            {
                                                thing.State = ThingState.Bouncing;
                                                thing.TouchedBy = playerId;
                                                thing.Hotness = 1;
                                                thing.FlashCount = 0;
                                            }
                                            else if ((thing.State == ThingState.Bouncing) && (fMs > 100.0))
                                            {
                                                hit |= HitType.Popped;
                                                int points = (pair.Key.Joint1 == JointType.FootLeft
                                                              || pair.Key.Joint1 == JointType.FootRight)
                                                                 ? 10
                                                                 : 5;
                                                this.AddToScore(
                                                    thing.TouchedBy,
                                                    points,
                                                    thing.Center);
                                                thing.TouchedBy = playerId;
                                            }
                                        }
                                    }

                                    this.things[i] = thing;

                                    if (thing.AvgTimeBetweenHits < 8)
                                    {
                                        hit |= HitType.Popped | HitType.Squeezed;
                                        if (this.gameMode != GameMode.Off)
                                        {
                                            this.AddToScore(playerId, 1, thing.Center);
                                        }
                                    }
                                }
                            }

                            break;
                    }

                    if ((hit & HitType.Popped) != 0)
                    {
                        thing.State = ThingState.Dissolving;
                        thing.Dissolve = 0;
                        thing.XVelocity = thing.YVelocity = 0;
                        thing.SpinRate = (thing.SpinRate * 6) + 0.2;
                        this.things[i] = thing;
                    }

                    allHits |= hit;
                }
            }

            return allHits;
        }

        //ADD AND MOVE THINGS
        public void AdvanceFrame()
        {

            if (stopped == 1) return;
            // Move all things by one step, accounting for gravity
            
            for (int thingIndex = 0; thingIndex < this.things.Count; thingIndex++)
            {
                Thing thing = this.things[thingIndex];
                thing.Center.Offset(thing.XVelocity, thing.YVelocity);
                thing.XVelocity = this.xvelocity;
                thing.YVelocity *= this.airFriction;
                thing.Theta += thing.SpinRate;

                if ((thing.Center.X + thing.Size > this.sceneRect.Width))
                {
                    this.things.Remove(this.things[thingIndex]);
                    TimeSpan span = DateTime.Now.Subtract(this.gameStartTime);
                    if (span.TotalSeconds > 2)
                    {
                        this.missed_blocks++;
                    }
                   
                    continue;
                }

                // Then get rid of one if any that fall off the bottom
                if (thing.Center.Y - thing.Size > this.sceneRect.Bottom)
                {
                    thing.State = ThingState.Remove;
                }

                // Get rid of after dissolving.
                if (thing.State == ThingState.Dissolving)
                {
                    thing.Dissolve += 1 / (this.targetFrameRate * DissolveTime);
                    thing.Size *= this.expandingRate;
                    if (thing.Dissolve >= 1.0)
                    {
                        thing.State = ThingState.Remove;
                    }
                }

                this.things[thingIndex] = thing;
            }
            
            // Then remove any that should go away now
            for (int i = 0; i < this.things.Count; i++)
            {
                Thing thing = this.things[i];
                if (thing.State == ThingState.Remove)
                {
                    this.things.Remove(thing);
                    i--;
                }
            }
            

            // Create any new things to drop based on dropRate
            if ((this.things.Count < this.maxThings) && (this.rnd.NextDouble() < this.dropRate / this.targetFrameRate) && (this.polyTypes != PolyType.None))
            {
                PolyType[] alltypes = 
                {
                    PolyType.Square 
                };
                byte r;
                byte g;
                byte b;

                if (this.doRandomColors)
                {
                    r = (byte)(this.rnd.Next(215) + 40);
                    g = (byte)(this.rnd.Next(215) + 40);
                    b = (byte)(this.rnd.Next(215) + 40);
                }
                else
                {
                    r = (byte)Math.Min(255.0, this.baseColor.R * (0.7 + (this.rnd.NextDouble() * 0.7)));
                    g = (byte)Math.Min(255.0, this.baseColor.G * (0.7 + (this.rnd.NextDouble() * 0.7)));
                    b = (byte)Math.Min(255.0, this.baseColor.B * (0.7 + (this.rnd.NextDouble() * 0.7)));
                }

                PolyType tryType;
                do
                {
                    tryType = alltypes[this.rnd.Next(alltypes.Length)];
                }
                while ((this.polyTypes & tryType) == 0);

                this.DropNewThing(tryType, this.shapeSize, System.Windows.Media.Color.FromRgb(r, g, b));
            }
            
        }
        public void DestroyThings()
        {

            for (int i = 0; i < this.things.Count; i++)
            {
                Thing thing = this.things[i];
                thing.State = ThingState.Dissolving;
                this.things[i] = thing;
                
            }
        }


        public void DrawFrame(UIElementCollection children)
        {
            this.frameCount++;
           
            // Draw all shapes in the scene
            for (int i = 0; i < this.things.Count; i++)
            {
                Thing thing = this.things[i];
                if (thing.Brush == null)
                {
                    thing.Brush = new SolidColorBrush(thing.Color);
                    double factor = 0.4 + (((double)thing.Color.R + thing.Color.G + thing.Color.B) / 1600);
                    thing.Brush2 =
                        new SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(
                                (byte)(255 - ((255 - thing.Color.R) * factor)),
                                (byte)(255 - ((255 - thing.Color.G) * factor)),
                                (byte)(255 - ((255 - thing.Color.B) * factor))));
                    thing.BrushPulse = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
                }

                if (thing.State == ThingState.Bouncing)
                {
                    // Pulsate edges
                    double alpha = Math.Cos((0.15 * (thing.FlashCount++) * thing.Hotness) * 0.5) + 0.5;

                    children.Add(
                        this.MakeSimpleShape(
                            this.polyDefs[thing.Shape].Sides,
                            this.polyDefs[thing.Shape].Skip,
                            thing.Size,
                            thing.Theta,
                            thing.Center,
                            thing.Brush,
                            thing.BrushPulse,
                            thing.Size * 0.1,
                            alpha,
                            thing.type,
                            thing.current_index
                            ));
                    
                    
                    this.things[i] = thing;
                }
                else
                {
                    if (thing.State == ThingState.Dissolving)
                    {
                        thing.Brush.Opacity = 1.0 - (thing.Dissolve * thing.Dissolve);
                    }

                    children.Add(
                        this.MakeSimpleShape(
                            this.polyDefs[thing.Shape].Sides,
                            this.polyDefs[thing.Shape].Skip,
                            thing.Size,
                            thing.Theta,
                            thing.Center,
                            thing.Brush,
                            (thing.State == ThingState.Dissolving) ? null : thing.Brush2, 1, 1,
                            thing.type,
                            thing.current_index));
                    if (this.stopped == 0 )
                    {
                        if (thing.type == enemy_type.ninja)
                        {
                            if(this.frameCount % 3 == 0) thing.current_index++;
                        }
                            
                        else if (thing.type == enemy_type.karate )
                        {
                            if(this.frameCount % 12 == 0) thing.current_index++;
                            
                        }
                        else if (thing.type == enemy_type.kroko)
                        {
                            if (this.frameCount % 15 == 0) thing.current_index++;

                        }
                        
                        else if (thing.type == enemy_type.black)
                        {
                            if (this.frameCount % 10 == 0) thing.current_index++;

                        }
                        else
                        {
                            thing.current_index++;
                        }
                        
                        
                    }
                    
                    this.things[i] = thing;
                }
            }

            // Show scores
            if (this.scores.Count != 0)
            {
                int i = 0;
                foreach (var score in this.scores)
                {
                    Label label = MakeSimpleLabel("Score: " +
                        score.Value.ToString(CultureInfo.InvariantCulture),
                        new Rect(
                            (0.02 + (i * 0.6)) * this.sceneRect.Width,
                            0.01 * this.sceneRect.Height,
                            0.4 * this.sceneRect.Width,
                            0.3 * this.sceneRect.Height), 
                            new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 255, 255, 255)));
                    label.FontSize = Math.Max(1, Math.Min(this.sceneRect.Width / 12, this.sceneRect.Height / 12));
                    children.Add(label);
                    i++;
                }
                
            }



            
            // Draw bonus
            string bonus_text = "Bonus: " + this.bonus.ToString(CultureInfo.InvariantCulture) + "/" + this.max_bonus.ToString(CultureInfo.InvariantCulture);
            Label bonus_label = MakeSimpleLabel(bonus_text, new Rect(this.sceneRect.Width * 0.4, 5, 0.4 * this.sceneRect.Width, 0.3 * this.sceneRect.Height), new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 255, 255, 255)));
            bonus_label.FontSize = Math.Max(1, Math.Min(this.sceneRect.Width / 20, this.sceneRect.Height / 20));
            children.Add(bonus_label);

            if (this.bonus >= this.max_bonus)
            {
                // voisi keksiä paremman nimen
                string weapon_ready_text = "Weapon ready! (shout 'Fire')";
                Label weapon_label = MakeSimpleLabel(weapon_ready_text, new Rect(this.sceneRect.Width * 0.4, 0.15 * this.sceneRect.Height, 0.4 * this.sceneRect.Width, 0.3 * this.sceneRect.Height), new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 255, 0, 0)));
                weapon_label.FontSize = Math.Max(1, Math.Min(20,20));
                children.Add(weapon_label);
            }
            
            // Draw missed blocks counter
            string missed_text = "Missed: " + this.missed_blocks.ToString(CultureInfo.InvariantCulture) + "/" + this.max_missed.ToString(CultureInfo.InvariantCulture);
            Label missed_label = MakeSimpleLabel(missed_text, new Rect(this.sceneRect.Width * 0.4, 50, 0.4 * this.sceneRect.Width, 0.3 * this.sceneRect.Height), new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 255, 255, 255)));
            missed_label.FontSize = Math.Max(1, Math.Min(this.sceneRect.Width / 20, this.sceneRect.Height / 20));
            children.Add(missed_label);

            if (this.missed_blocks >= this.max_missed)
            {
                string game_over_text = "Game Over! Say 'reset' to start again";
                Label game_over_label = MakeSimpleLabel(game_over_text, new Rect(this.sceneRect.Width * 0.02, 0.5 * this.sceneRect.Height, 0.98 * this.sceneRect.Width, 0.3 * this.sceneRect.Height), new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 0, 0, 255)));
                game_over_label.FontSize = Math.Max(1, Math.Min(65, 65));
                children.Add(game_over_label);
                this.stopped = 1;
            }



            // Show game timer
            if (this.gameMode != GameMode.Off)
            {
                TimeSpan span = DateTime.Now.Subtract(this.gameStartTime);
                string text = span.Minutes.ToString(CultureInfo.InvariantCulture) + ":" + span.Seconds.ToString("00");

                Label timeText = MakeSimpleLabel(
                    text,
                    new Rect(
                        0.1 * this.sceneRect.Width, 0.25 * this.sceneRect.Height, 0.89 * this.sceneRect.Width, 0.72 * this.sceneRect.Height),
                    new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 255, 255, 255)));
                timeText.FontSize = Math.Max(1, this.sceneRect.Height / 16);
                timeText.HorizontalContentAlignment = HorizontalAlignment.Right;
                timeText.VerticalContentAlignment = VerticalAlignment.Bottom;
                children.Add(timeText);
            }
        }

        private static double SquaredDistance(double x1, double y1, double x2, double y2)
        {
            return ((x2 - x1) * (x2 - x1)) + ((y2 - y1) * (y2 - y1));
        }

        private void AddToScore(int player, int points, System.Windows.Point center)
        {
            if (this.scores.ContainsKey(player))
            {
                this.scores[player] = this.scores[player] + points;
                
            }
            else
            {
                this.scores.Add(player, points);
            }

            if (this.bonus < this.max_bonus && this.missed_blocks < this.max_missed)
            {
                this.bonus++;
            }

            FlyingText.NewFlyingText(this.sceneRect.Width / 300, center, "+" + points);
        }

        private void DropNewThing(PolyType newShape, double newSize, System.Windows.Media.Color newColor)
        {
            // Only drop within the center "square" area 
            double dropWidth = this.sceneRect.Bottom - this.sceneRect.Top;
            if (dropWidth > this.sceneRect.Right - this.sceneRect.Left)
            {
                dropWidth = this.sceneRect.Right - this.sceneRect.Left;
            }

            var newThing = new Thing
            {
                Size = newSize,
                YVelocity = ((0.5 * this.rnd.NextDouble()) - 0.25) / this.targetFrameRate,
                XVelocity = 0.5,
                Shape = newShape,
           
                Center = new System.Windows.Point(25.0 , this.rnd.NextDouble() * dropWidth),
                SpinRate = 0,//((this.rnd.NextDouble() * 12.0) - 6.0) * 2.0 * Math.PI / this.targetFrameRate / 4.0,
                Theta = 3.14/4, // laittaa neliöt suoraan
                TimeLastHit = DateTime.MinValue,
                AvgTimeBetweenHits = 100,
                Color = newColor,
                Brush = null,
                Brush2 = null,
                BrushPulse = null,
                Dissolve = 0,
                State = ThingState.Falling,
                TouchedBy = 0,
                Hotness = 0,
                FlashCount = 0

            };

             
            Array values = Enum.GetValues(typeof(enemy_type));
            Random random = new Random();
            newThing.type = (enemy_type)values.GetValue(random.Next(values.Length));


            this.things.Add(newThing);
        }

        private Shape MakeSimpleShape(
            int numSides,
            int skip,
            double size,
            double spin,
            System.Windows.Point center,
            System.Windows.Media.Brush brush,
            System.Windows.Media.Brush brushStroke,
            double strokeThickness,
            double opacity,
            enemy_type type,
            int current_image)
        {
            if (numSides <= 1)
            {
                var circle = new Ellipse { Width = size * 2, Height = size * 2, Stroke = brushStroke };
                if (circle.Stroke != null)
                {
                    circle.Stroke.Opacity = opacity;
                }

                circle.StrokeThickness = strokeThickness * ((numSides == 1) ? 1 : 2);
                circle.Fill = (numSides == 1) ? brush : null;
                circle.SetValue(Canvas.LeftProperty, center.X - size);
                circle.SetValue(Canvas.TopProperty, center.Y - size);
                return circle;
            }

            var points = new PointCollection(numSides + 2);
            double theta = spin;
            for (int i = 0; i <= numSides + 1; ++i)
            {
                points.Add(new System.Windows.Point((Math.Cos(theta) * size) + center.X, (Math.Sin(theta) * size) + center.Y));
                theta = theta + (2.0 * Math.PI * skip / numSides);
            }

            var polyline = new Polyline { Points = points, Stroke = brushStroke };
            if (polyline.Stroke != null)
            {
                polyline.Stroke.Opacity = opacity;
            }

            

            //polyline.Fill = brush;

            if (type == enemy_type.Orc)
            {
                polyline.Fill = new ImageBrush(orc);
            }
            else if (type == enemy_type.basic)
            {
                //Random r = new Random();
                //int s = r.Next(0, 26);
                
                polyline.Fill = new ImageBrush(timlist[current_image%27]);
            }
            else if (type == enemy_type.ninja)
            {
                
                polyline.Fill = new ImageBrush(ninjalist[current_image % 13]);
            }
            else if (type == enemy_type.karate)
            {

                polyline.Fill = new ImageBrush(karatelist[current_image % 81]);
            }
            else if (type == enemy_type.kroko)
            {

                polyline.Fill = new ImageBrush(krokolist[current_image % 16]);
            }
            else if (type == enemy_type.black)
            {

                polyline.Fill = new ImageBrush(blacklist[current_image % 21]);
            }
            polyline.FillRule = FillRule.Nonzero;
            polyline.StrokeThickness = 0;//strokeThickness; // =0;
            
            return polyline;
        }

        internal struct PolyDef
        {
            public int Sides;
            public int Skip;
        }

        // The Thing struct represents a single object that is flying through the air, and
        // all of its properties.
        private struct Thing
        {
            public System.Windows.Point Center;
            public double Size;
            public double Theta;
            public double SpinRate;
            public double YVelocity;
            public double XVelocity;
            public PolyType Shape;
            public System.Windows.Media.Color Color;
            public System.Windows.Media.Brush Brush;
            public System.Windows.Media.Brush Brush2;
            public System.Windows.Media.Brush BrushPulse;
            public double Dissolve;
            public ThingState State;
            public DateTime TimeLastHit;
            public double AvgTimeBetweenHits;
            public int TouchedBy;               // Last player to touch this thing
            public int Hotness;                 // Score level
            public int current_index;
            public int FlashCount;
            public enemy_type type;
            

            // Hit testing between this thing and a single segment.  If hit, the center point on
            // the segment being hit is returned, along with the spot on the line from 0 to 1 if
            // a line segment was hit.
            public bool Hit(Segment seg, ref System.Windows.Point hitCenter, ref double lineHitLocation)
            {
                double minDxSquared = this.Size + seg.Radius;
                minDxSquared *= minDxSquared;

                // See if falling thing hit this body segment
                if (seg.IsCircle())
                {
                    if (SquaredDistance(this.Center.X, this.Center.Y, seg.X1, seg.Y1) <= minDxSquared)
                    {
                        hitCenter.X = seg.X1;
                        hitCenter.Y = seg.Y1;
                        lineHitLocation = 0;
                        return true;
                    }
                }
                else
                {
                    double sqrLineSize = SquaredDistance(seg.X1, seg.Y1, seg.X2, seg.Y2);
                    if (sqrLineSize < 0.5)
                    {
                        // if less than 1/2 pixel apart, just check dx to an endpoint
                        return SquaredDistance(this.Center.X, this.Center.Y, seg.X1, seg.Y1) < minDxSquared;
                    }

                    // Find dx from center to line
                    double u = ((this.Center.X - seg.X1) * (seg.X2 - seg.X1)) + (((this.Center.Y - seg.Y1) * (seg.Y2 - seg.Y1)) / sqrLineSize);
                    if ((u >= 0) && (u <= 1.0))
                    {   // Tangent within line endpoints, see if we're close enough
                        double intersectX = seg.X1 + ((seg.X2 - seg.X1) * u);
                        double intersectY = seg.Y1 + ((seg.Y2 - seg.Y1) * u);

                        if (SquaredDistance(this.Center.X, this.Center.Y, intersectX, intersectY) < minDxSquared)
                        {
                            lineHitLocation = u;
                            hitCenter.X = intersectX;
                            hitCenter.Y = intersectY;
                            return true;
                        }
                    }
                    else
                    {
                        // See how close we are to an endpoint
                        if (u < 0)
                        {
                            if (SquaredDistance(this.Center.X, this.Center.Y, seg.X1, seg.Y1) < minDxSquared)
                            {
                                lineHitLocation = 0;
                                hitCenter.X = seg.X1;
                                hitCenter.Y = seg.Y1;
                                return true;
                            }
                        }
                        else
                        {
                            if (SquaredDistance(this.Center.X, this.Center.Y, seg.X2, seg.Y2) < minDxSquared)
                            {
                                lineHitLocation = 1;
                                hitCenter.X = seg.X2;
                                hitCenter.Y = seg.Y2;
                                return true;
                            }
                        }
                    }

                    return false;
                }

                return false;
            }

            // Change our velocity based on the object's velocity, our velocity, and where we hit.
            public void BounceOff(double x1, double y1, double otherSize, double fXv, double fYv)
            {
                double x0 = this.Center.X;
                double y0 = this.Center.Y;
                double xv0 = this.XVelocity - fXv;
                double yv0 = this.YVelocity - fYv;
                double dist = otherSize + this.Size;
                double dx = Math.Sqrt(((x1 - x0) * (x1 - x0)) + ((y1 - y0) * (y1 - y0)));
                double xdif = x1 - x0;
                double ydif = y1 - y0;
                double newvx1 = 0;
                double newvy1 = 0;

                x0 = x1 - (xdif / dx * dist);
                y0 = y1 - (ydif / dx * dist);
                xdif = x1 - x0;
                ydif = y1 - y0;

                double bsq = dist * dist;
                double b = dist;
                double asq = (xv0 * xv0) + (yv0 * yv0);
                double a = Math.Sqrt(asq);
                if (a > 0.000001)
                {
                    // if moving much at all...
                    double cx = x0 + xv0;
                    double cy = y0 + yv0;
                    double csq = ((x1 - cx) * (x1 - cx)) + ((y1 - cy) * (y1 - cy));
                    double tt = asq + bsq - csq;
                    double bb = 2 * a * b;
                    double power = a * (tt / bb);
                    newvx1 -= 2 * (xdif / dist * power);
                    newvy1 -= 2 * (ydif / dist * power);
                }

                this.XVelocity += newvx1;
                this.YVelocity += newvy1;
                this.Center.X = x0;
                this.Center.Y = y0;
            }
        }
    }
}
