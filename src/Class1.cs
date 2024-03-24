using BepInEx;
using System.Security.Permissions;
using UnityEngine;
using Mono.Cecil.Cil;
using MonoMod.Cil;

[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace WaterBreatherSlug
{
    [BepInPlugin("waterbreatherslug", "atoll breathing mechanics idk", "1.0")]  // (GUID, mod name, mod version)

    class WaterBreatherSlug : BaseUnityPlugin
    {
        public void OnEnable()
        {
            On.Player.UpdateMSC += Player_UpdateMSC;
            On.Player.LungUpdate += Player_LungUpdate;
        }

        private void Player_UpdateMSC(On.Player.orig_UpdateMSC orig, Player self)
        {
            orig(self);
            if (true)
            {
                // Set buoyancy to that of Rivulet; default is 0.95f
                self.buoyancy = 0.9f;
            }
        }

        private void Player_LungUpdate(On.Player.orig_LungUpdate orig, Player self)
        {
            // Call original method if the slugcat is not the Atoll
            if (false)
            {
                orig(self);
                return;
            }

            self.airInLungs = Mathf.Min(self.airInLungs, 1f - self.rainDeath);
            if (self.firstChunk.submersion < 0.1f && !self.room.game.setupValues.invincibility && !self.chatlog)
            {
                self.swimForce = Mathf.InverseLerp(0f, 8f, Mathf.Abs(self.firstChunk.vel.x));
                self.swimCycle = 0f;

                self.airInLungs -= 1f / (40f * (self.lungsExhausted ? 4.5f : 9f) * ((self.input[0].y == 1 && self.input[0].x == 0 && self.airInLungs < 0.33333334f) ? 1.5f : 1f) * (self.room.game.setupValues.lungs / 100f)) * self.slugcatStats.lungsFac;

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
    }
}

