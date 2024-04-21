using System;
using System.IO;
using System.Runtime.CompilerServices;
using BepInEx;
using MoreSlugcats;
using On;
using SlugBase.Features;
using SlugBase.DataTypes;
using static SlugBase.Features.FeatureTypes;
using UnityEngine;
using RWCustom;
using MonoMod.Cil;
using System.Linq;
using System.Collections.Generic;
using static MonoMod.InlineRT.MonoModRule;
using IL;
using System.Reflection;

namespace AzureTapestry
{
    [BepInPlugin(MOD_ID, "The Atoll", "0.1.0")]
    class Plugin : BaseUnityPlugin
    {
        
        private const string MOD_ID = "azureTeam.azureTapestry";
        public static readonly RoomRain.DangerType Drain = new RoomRain.DangerType("Drain", true);
        public static readonly SlugcatStats.Name AtollName = new SlugcatStats.Name("azureTeam.Atoll", false);
        public static readonly SlugcatStats.Name FRName = new SlugcatStats.Name("azureTeam.FloodRaiser", false);
        public bool isUnderwaterEater = false;
        private RoomSettings.RoomEffect.Type[] roomRainSettings = { RoomSettings.RoomEffect.Type.HeavyRain, RoomSettings.RoomEffect.Type.LightRain, RoomSettings.RoomEffect.Type.BulletRain};
        
        // Add hooks
        public void OnEnable()
        {
            On.RainWorld.OnModsInit += Extras.WrapInit(LoadResources);
            On.Player.Update += waterStats;
            On.Player.UpdateMSC += changeBuoy;
            On.Player.LungUpdate += easyBreather;
            On.Weapon.Thrown += weaponVelocityMultWater;
            On.Player.ctor += initLungs;
            On.Player.LungUpdate += waterBreather;
            On.Player.CanIPickThisUp += spearPuller;
            On.Creature.Update += karmaLavaShield;
            IL.Player.GrabUpdate += underwaterConsumption;
            On.Creature.Violence += damageMultWater;
            On.DaddyCorruption.BulbNibbleAtChunk += noEatingChunks;
            On.DaddyCorruption.CorruptionLevel_IntVector2 += purifyIntVector2;
            On.DaddyCorruption.CorruptionLevel_Vector2 += purifyVector2;
            On.DaddyCorruption.LittleLeg.Update += punyLittleLegs;
            On.DaddyCorruption.Bulb.Update += weakFragileTeeth;
            //On.DaddyCorruption.Bulb.ctor += blockEyes;
            // Put your custom hooks here!
        }

        private bool isAzureCat(Player self)
        {
            return self.slugcatStats.name == AtollName || self.slugcatStats.name == FRName;
        }

        private bool isAtoll(Player self)
        {
            return self.slugcatStats.name == AtollName;
        }

        private bool isFloodRaiser(Player self)
        {
             return self.slugcatStats.name == FRName;
        }

        private void underwaterConsumption(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            c.GotoNext(
                x => x.MatchLdarg(0),
                x => x.MatchCall<Player>("get_isRivulet"));
            c.Index += 2;
            c.EmitDelegate<Func<bool, bool>>(isRiv => isRiv || isUnderwaterEater);
        }

        private void waterBreather(On.Player.orig_LungUpdate orig, Player self)
        {
            // Call original method if the player isn't the Atoll
            if (!isAtoll(self))
            {
                orig(self);
                return;
            }
            if (self.firstChunk.submersion < 0.1f && !isHumidEnough(self.room) && !self.room.game.setupValues.invincibility && !self.chatlog)
            {
                self.swimForce = Mathf.InverseLerp(0f, 8f, Mathf.Abs(self.firstChunk.vel.x));
                self.swimCycle = 0f;

                self.airInLungs -= 1f / (40f * (self.lungsExhausted ? 4.5f : 9f) * ((self.input[0].y == 1 && self.input[0].x == 0 && self.airInLungs < 0.33333334f) ? 1.5f : 1f) * (self.room.game.setupValues.lungs / 100f)) * self.slugcatStats.lungsFac;
                if (isExtraDry(self)) //breath is lost twice as fast in extra dry rooms
                {
                    self.airInLungs -= 1f / (40f * (self.lungsExhausted ? 4.5f : 9f) * ((self.input[0].y == 1 && self.input[0].x == 0 && self.airInLungs < 0.33333334f) ? 1.5f : 1f) * (self.room.game.setupValues.lungs / 100f)) * self.slugcatStats.lungsFac;
                }

                if (self.airInLungs <= 0f && self.mainBodyChunk.submersion == 0f && self.bodyChunks[1].submersion < 0.5f)
                {
                    self.airInLungs = 0f;
                    self.Stun(10);
                    self.drown += 0.008333334f;
                    if (self.drown >= 1f)
                    {
                        self.Die();
                    }
                }
                else if (self.airInLungs < 0.4f)
                {
                    self.lungsExhausted = true;
                    if (self.slowMovementStun < 1)
                    {
                        self.slowMovementStun = 1;
                    }
                    self.bodyChunks[1].vel *= Mathf.Lerp(1f, 0.9f, Mathf.InverseLerp(0f, 0.4f, self.airInLungs));
                }
                self.submerged = false;
            }
            else
            {
                if (!self.lungsExhausted && self.airInLungs > 0.9f)
                {
                    self.airInLungs = 1f;
                }
                if (self.airInLungs <= 0f)
                {
                    self.airInLungs = 0f;
                }
                self.airInLungs += 1f / (self.lungsExhausted ? 240 : 60);
                if (self.airInLungs >= 1f)
                {
                    self.airInLungs = 1f;
                    self.lungsExhausted = false;
                    self.drown = 0f;
                }
                self.submerged = true;
            }
            if (self.lungsExhausted)
            {
                if (self.slowMovementStun < 5)
                {
                    self.slowMovementStun = 5;
                }
                if (self.drown > 0f && self.slowMovementStun < 10)
                {
                    self.slowMovementStun = 10;
                }
            }
        }

        private void easyBreather(On.Player.orig_LungUpdate orig, Player self)
        {
            orig(self);
            if (isFloodRaiser(self))
            {
                self.airInLungs = 1f;
            }
        }
        
        private void changeBuoy(On.Player.orig_UpdateMSC orig, Player self)
        {
            orig(self);
            if (isAzureCat(self)) 
            {
                self.buoyancy = 0.9f;
            }
        }

        // Load any resources, such as sprites or sounds
        private void LoadResources(RainWorld rainWorld)
        {
        }

        private bool isHumidEnough(Room room)
        {
            if (room.world.region.name == "LC")
            {
                return true;
            }
            for(int i = 0; i < 3; i++)
            {
                if (room.roomSettings.GetEffectAmount(roomRainSettings[i]) > 0.6f) return true;
            }
            return false;
        }

        private bool isExtraDry(Player self)
        {
            string name = self.room.world.region.name;
            return self.room.world.game.StoryCharacter == AtollName && (name == "DM" || name == "SH" || name == "HR");
        }

        private void initLungs(On.Player.orig_ctor orig, Player self, AbstractCreature creature, World world)
        {
            orig(self, creature, world);
            if(isAtoll(self)) self.slugcatStats.lungsFac = 0.15f;
            isUnderwaterEater = isAzureCat(self);
        }
        private bool spearPuller(On.Player.orig_CanIPickThisUp orig, Player self, PhysicalObject obj)
        {
            if(obj is Spear s && s.mode == Weapon.Mode.StuckInWall && isAtoll(self))
            {
                return self.submerged || isHumidEnough(self.room);
            }
            return orig(self, obj);
        }

        /*void inverseFlood(On.RoomRain.orig_Update orig, RoomRain self, bool eu)
        {
            if(!ModManager.MSC || !(self.dangerType == Plugin.Drain))
            {
                orig(self, eu);
                return;
            }
            orig(self, eu);
            return;
        }

        void restoreWater(On.Room.orig_Loaded orig, Room self)
        {
            orig(self);
            if(ModManager.MSC && self.game.globalRain.drainWorldFlood > 0f && (self.roomSettings.DangerType == Plugin.Drain) && self.waterObject != null) {
                return;
            }
            return;
        }*/

        void waterStats(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);
            if(isAzureCat(self))
            {
                self.slugcatStats.loudnessFac *= self.submerged? 0.6f : 1.4f;
                self.slugcatStats.generalVisibilityBonus += self.submerged ? -0.1f * self.slugcatStats.generalVisibilityBonus : 0.2f * self.slugcatStats.generalVisibilityBonus;
                if (self.room.IsGateRoom()) self.airInLungs = 1f;
            }
            
        }
        
        void karmaLavaShield(On.Creature.orig_Update orig, Creature self, bool eu)
        {
            orig(self, eu);
            if(self is Player player && isAzureCat(player))
            {
                self.abstractCreature.lavaImmune = player.KarmaCap >= 9;
            }
        }

        void weaponVelocityMultWater(On.Weapon.orig_Thrown orig, Weapon weapon, Creature creature, Vector2 thrownPos, Vector2? firstFrameTraceFromPos, IntVector2 throwDir, float frc, bool eu)
        {
            orig(weapon, creature, thrownPos, firstFrameTraceFromPos, throwDir, frc, eu);
            if (creature is Player player)
            {
                if (isAtoll(player)) {
                    weapon.firstChunk.vel *= player.submerged ? 4f : 0.8f;
                }
                if (isFloodRaiser(player)) 
                {
                    weapon.firstChunk.vel *= player.submerged ? 2f : 1f;
                }
            }
        }

        void damageMultWater(On.Creature.orig_Violence orig, Creature self, BodyChunk source, UnityEngine.Vector2? directionAndMomentum, BodyChunk hitChunk, PhysicalObject.Appendage.Pos hitAppendage, Creature.DamageType type, float damage, float stunBonus)
        {
            if(source.owner is Player player && isAzureCat(player))
            {
                damage *= isAtoll(player) && player.submerged ? 3f : 0.3f;
                damage *= isFloodRaiser(player) && player.submerged ? 1.2f : 0.8f;
            }
            orig(self, source, directionAndMomentum, hitChunk, hitAppendage, type, damage, stunBonus);
        }

        private static void noEatingChunks(On.DaddyCorruption.orig_BulbNibbleAtChunk orig, DaddyCorruption self, DaddyCorruption.Bulb bulb, BodyChunk chunk)
        {
            if(self.room.world.game.IsStorySession && (self.room.world.game.GetStorySession.characterStats.name == AtollName || self.room.world.game.GetStorySession.characterStats.name == FRName))
            {
                return;
            }
            orig(self, bulb, chunk);
        }

        private static float purifyIntVector2(On.DaddyCorruption.orig_CorruptionLevel_IntVector2 orig, DaddyCorruption self, IntVector2 iv)
        {
            if (self.room.world.game.IsStorySession && (self.room.world.game.GetStorySession.characterStats.name == AtollName || self.room.world.game.GetStorySession.characterStats.name == FRName))
            {
                return 0.75f;
            }
            else
            {
                return orig(self, iv);
            }
        }
        private static float purifyVector2(On.DaddyCorruption.orig_CorruptionLevel_Vector2 orig, DaddyCorruption self, Vector2 iv)
        {
            if (self.room.world.game.IsStorySession && (self.room.world.game.GetStorySession.characterStats.name == AtollName || self.room.world.game.GetStorySession.characterStats.name == FRName))
            {
                return 0.75f;
            }
            else
            {
                return orig(self, iv);
            }
        }

        private static void punyLittleLegs(On.DaddyCorruption.LittleLeg.orig_Update orig, DaddyCorruption.LittleLeg self, bool eu)
        {
            if (self.owner.room.game.IsStorySession && (self.owner.room.game.GetStorySession.characterStats.name == AtollName || self.owner.room.game.GetStorySession.characterStats.name == FRName))
            {
                //typecasting
                self.evenUpdate = eu;
                for(int i = 2; i < self.segments.GetLength(0); i++)
                {
                    Vector2 a = Custom.DirVec(self.segments[i - 2, 0], self.segments[i, 0]);
                    self.segments[i - 2, 2] -= a * self.pushApart;
                    self.segments[i, 2] += a * self.pushApart;
                    for (int j = 0; j < self.segments.GetLength(0); j++)
                    {
                        self.segments[j, 2].y -= 0.9f * self.room.gravity * self.GravityAffected(j);
                        self.segments[j, 1] = self.segments[j, 0];
                        self.segments[j, 0] += self.segments[j, 2];
                        self.segments[j, 2] *= 0.999f;
                        if (self.room.gravity < 1f && self.room.readyForAI && self.room.aimap.getTerrainProximity(self.segments[j, 0]) < 4)
                        {
                            IntVector2 tilePosition = self.room.GetTilePosition(self.segments[j, 0]);
                            Vector2 a2 = new Vector2(0f, 0f);
                            for (int k = 0; k < 4; k++)
                            {
                                if (!self.room.GetTile(tilePosition + Custom.fourDirections[k]).Solid && !self.room.aimap.getAItile(tilePosition + Custom.fourDirections[k]).narrowSpace)
                                {
                                    float num = 0f;
                                    for (int l = 0; l < 4; l++)
                                    {
                                        num += (float)self.room.aimap.getTerrainProximity(tilePosition + Custom.fourDirections[k] + Custom.fourDirections[l]);
                                    }
                                    a2 += Custom.fourDirections[k].ToVector2() * num;
                                }
                            }
                            self.segments[j, 2] += a2.normalized * (self.room.GetTile(self.segments[j, 0]).Solid ? 1f : Custom.LerpMap((float)self.room.aimap.getTerrainProximity(self.segments[j, 0]), 0f, 3f, 2f, 0.2f)) * (1f - self.room.gravity);
                        }
                        if (j > 2 && self.room.aimap.getTerrainProximity(self.segments[j, 0]) < 3)
                        {
                            SharedPhysics.TerrainCollisionData terrainCollisionData = self.scratchTerrainCollisionData.Set(self.segments[j, 0], self.segments[j, 1], self.segments[j, 2], 2f, new IntVector2(0, 0), true);
                            terrainCollisionData = SharedPhysics.VerticalCollision(self.room, terrainCollisionData);
                            terrainCollisionData = SharedPhysics.HorizontalCollision(self.room, terrainCollisionData);
                            self.segments[j, 0] = terrainCollisionData.pos;
                            self.segments[j, 2] = terrainCollisionData.vel;
                            if (terrainCollisionData.contactPoint.x != 0)
                            {
                                self.segments[j, 2].y *= 0.6f;
                            }
                            if (terrainCollisionData.contactPoint.y != 0)
                            {
                                self.segments[j, 2].x *= 0.6f;
                            }
                        }
                    }
                    self.ConnectToWalls();
                    for (int m = self.segments.GetLength(0) - 1; m > 0; m--)
                    {
                        self.Connect(m, m - 1);
                    }
                    self.ConnectToWalls();
                    for (int n = 1; n < self.segments.GetLength(0); n++)
                    {
                        self.Connect(n, n - 1);
                    }
                    self.ConnectToWalls();
                    self.graphic.Update();
                }
                //end typecasting
                for (int i = 0; i < self.segments.GetLength(0); i++)
                {
                    float d = (float)i / (float)(self.segments.GetLength(0) - 1);
                    self.segments[i, 2] += self.mountedDir * Mathf.InverseLerp(5f, 1f, (float)i);
                    if (self.myBulb.legReachPos != null)
                    {
                        self.segments[i, 2] += Custom.DirVec(self.segments[i, 0], self.myBulb.legReachPos.Value) * 0.2f * UnityEngine.Random.value;
                    }
                    else if (self.moveCounter < 0)
                    {
                        self.segments[i, 2] += Custom.RNV() * 2f * UnityEngine.Random.value * d;
                    }
                }
                self.moveCounter--;
                if (self.moveCounter < 0 && UnityEngine.Random.value < 0.025f)
                {
                    self.moveCounter = UnityEngine.Random.Range(80, 300);
                }
            }
            else
            {
                orig(self, eu);
            }
        }

        private static void weakFragileTeeth(On.DaddyCorruption.Bulb.orig_Update orig, DaddyCorruption.Bulb self)
        {
            if (self.owner.room.game.IsStorySession && (self.owner.room.game.GetStorySession.characterStats.name == AtollName || self.owner.room.game.GetStorySession.characterStats.name == FRName))
            {
                self.lastPos = self.pos;
                self.pos += self.vel;
                self.vel *= 0.9f;
                self.vel += self.lookDir * 0.1f;
                self.vel -= (self.pos - self.stuckPos) / 10f;
                self.lastClosed = self.closed;
                if (self.bubblesWait > 0)
                {
                    self.bubblesWait--;
                }
                if (!Custom.DistLess(self.pos, self.stuckPos, self.rad / 2f))
                {
                    self.vel -= (self.pos - self.stuckPos).normalized * (Vector2.Distance(self.pos, self.stuckPos) - self.rad / 2f);
                    self.pos -= (self.pos - self.stuckPos).normalized * (Vector2.Distance(self.pos, self.stuckPos) - self.rad / 2f);
                }
                if (self.leg != null)
                {
                    self.vel += Custom.DirVec(self.pos, self.leg.segments[Custom.IntClamp(self.leg.segments.GetLength(0) / 2, 0, self.leg.segments.GetLength(0) - 1), 0]);
                }
                self.closed = Mathf.Max(0f, self.closed - 0.05f);
                float num6 = self.light * Mathf.InverseLerp(0f, 1f, Vector2.Distance(self.lastLookDir, self.lookDir));
                self.light = Mathf.Max(0f, self.light - 0.05f);
                if (UnityEngine.Random.value < num6)
                {
                    self.getToFocus = Mathf.Max(self.getToFocus, UnityEngine.Random.value);
                }
                else if (UnityEngine.Random.value < 0.014285714f)
                {
                    self.getToFocus = 0f;
                }
                self.lastFocus = self.focus;
                if (self.focus < self.getToFocus)
                {
                    self.focus = Mathf.Min(self.focus + 0.05f, self.getToFocus);
                }
                else
                {
                    self.focus = Mathf.Max(self.focus - 0.05f, self.getToFocus);
                }
                if (UnityEngine.Random.value < 0.01f)
                {
                    self.legReachPos = null;
                }
                self.lastLookDir = self.lookDir;
                if (self.reactionDelay < 1)
                {
                    self.lookDir = self.nextLookDir;
                    self.reactionDelay = UnityEngine.Random.Range(10, 20);
                }
                else
                {
                    self.reactionDelay--;
                }
                if (UnityEngine.Random.value < 0.00125f)
                {
                    self.nextLookDir = Custom.RNV();
                }
            }
            else
            {
                orig(self);
            }
        }
    }

    class DOIDeadCorruptRoomSpecificScript : UpdatableAndDeletable
    {
        public DOIDeadCorruptRoomSpecificScript(Room room)
        {
            this.room = room;
            
        }
        public bool hasDaddyObj()
        {
            bool flag = false;
            foreach (UpdatableAndDeletable obj in this.room.updateList)
            {
                if (obj is DaddyCorruption)
                {
                    flag = true;
                    break;
                }
            }
            return flag;
        }
    }

    static class ExtensionMethods
    {
        
    }
}