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

namespace SlugTemplate
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
            if (room.world.region.name == "UJ")
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
            return self.room.world.game.StoryCharacter == AtollName && (name == "DM" || name == "SH");
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
                return self.submerged;
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
            if(creature is Player player)
            {
                if (isAtoll(player)) weapon.firstChunk.vel *= player.submerged ? 4f : 0.8f;
                if (isFloodRaiser(player)) weapon.firstChunk.vel *= player.submerged ? 2f : 1f;
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
    }
}