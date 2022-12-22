using Celeste.Mod;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Microsoft.Xna;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using On.Celeste;
using Mono.Cecil.Cil;
using Steamworks;
using Celeste.Mod.Entities;

namespace Celeste.Mod.TimeMechanic {
    public class TimeMechanic : EverestModule {
        List<RewindStateInfo> rewinder = new List<RewindStateInfo>();
        public static Player thePlayer;
        public static bool hasStartedToDash;
        public override void LoadSettings() {

        }

        public override void Load() {
            rewinder = new List<RewindStateInfo>();
            IL.Celeste.Player.ctor += Player_ctor;
            On.Celeste.Player.ctor += Player_ctor1;
            On.Celeste.Player.Update += Player_Update;
            On.Celeste.Player.UpdateSprite += Player_UpdateSprite;
            On.Celeste.Player.StartDash += Player_StartDash;
        }

        private int Player_StartDash(On.Celeste.Player.orig_StartDash orig, Player self)
        {
            hasStartedToDash = true;
            return orig(self);
        }

        private void Player_UpdateSprite(On.Celeste.Player.orig_UpdateSprite orig, Player self)
        {
            if (thePlayer.StateMachine.State != 26)
            {
                orig(self);
            }
        }
        private void CaptureParticleState(Player p)
        {

        }

        private void CaptureBurstState(Player p, RewindStateInfo state)
        {
            List<RewindStateInfo.BurstStateInfo> bs = new List<RewindStateInfo.BurstStateInfo>();

            DisplacementRenderer dispRenderer = thePlayer.SceneAs<Level>().Displacement;
            //get the shockwave points list
            List<DisplacementRenderer.Burst> points = (List<DisplacementRenderer.Burst>)typeof(DisplacementRenderer).GetField("points", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dispRenderer);
            for (int i = 0; i < points.Count; i++)
            {
                RewindStateInfo.BurstStateInfo fakeBurst = new RewindStateInfo.BurstStateInfo();
                DisplacementRenderer.Burst realBurst = points[i];
                //make a bit of a copy
                fakeBurst.scaleFrom = realBurst.ScaleFrom;
                fakeBurst.scaleTo = realBurst.ScaleTo;
                fakeBurst.texture = realBurst.Texture;
                fakeBurst.position = realBurst.Position;
                fakeBurst.duration = realBurst.Duration;
                fakeBurst.percent = realBurst.Percent;
                fakeBurst.alphaFrom = realBurst.AlphaFrom;
                fakeBurst.alphaTo = realBurst.AlphaTo;
                fakeBurst.alphaEaser = realBurst.AlphaEaser;
                bs.Add(fakeBurst);
            }
            state.burstState = bs;
        }

        private void Player_Update(On.Celeste.Player.orig_Update orig, Player p)
        {

            if (thePlayer.StateMachine.State != 26)
            {
                if (rewinder.Count >= 10 * 60)
                {
                    rewinder.RemoveAt(0);
                }
                RewindStateInfo state = new RewindStateInfo(
                        p.Position,
                        p.Facing,
                        p.Sprite.CurrentAnimationID,
                        p.Sprite.CurrentAnimationFrame,
                        p.Hair.Nodes.GetRange(0, p.Hair.Nodes.Count)
                        );
                Sprite sweatSprite = (Sprite)typeof(Player).GetField("sweatSprite", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(p);
                state.sweatSprite = sweatSprite.CurrentAnimationID;
                state.sweatFrame = sweatSprite.CurrentAnimationFrame;
                state.spriteRef = sweatSprite; //dont use reflection again, just save a reference 
                state.scale = p.Sprite.Scale;
                state.dashDir = p.Speed.Angle(); // the dashDir variable is only set when the statemachine component updates (later)

                ParticleSystem fg = thePlayer.SceneAs<Level>().ParticlesFG;
                Particle[] farticles = (Particle[])typeof(ParticleSystem).GetField("particles", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(fg);
                state.farticles = (Particle[])farticles.Clone();

                ParticleSystem part = thePlayer.SceneAs<Level>().Particles;
                Particle[] particles = (Particle[])typeof(ParticleSystem).GetField("particles", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(part);
                state.particles = (Particle[])particles.Clone();

                ParticleSystem bg = thePlayer.SceneAs<Level>().ParticlesBG;
                Particle[] barticles = (Particle[])typeof(ParticleSystem).GetField("particles", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bg);
                state.barticles = (Particle[])barticles.Clone();

                state.dash = hasStartedToDash;

                state.playerSpeed = p.Speed;
                hasStartedToDash = false;
                //CaptureBurstState(p,state);
                rewinder.Add(state);

                //self.Sprite.Rate = -1;
            }

            if (Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.N))
            {
                p.StateMachine.State = 26;
                //TrailManager.Add(thePlayer, thePlayer.GetCurrentTrailColor(),duration:0.5f);
            }
            if (Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.M))
            {
                thePlayer.SceneAs<Level>().Add(new TimeEnt(p.Position));
            }
            orig(p);
        }

        private void Player_ctor1(On.Celeste.Player.orig_ctor orig, Player self, Vector2 position, PlayerSpriteMode spriteMode)
        {
            orig(self, position, spriteMode);
            thePlayer = self;
            self.StateMachine.SetCallbacks(26, new Func<int>(this.RewindTime));
        }

        private void Player_ctor(MonoMod.Cil.ILContext il) //change the StateMachine constructor's parameter to 1 more so we can make a state for rewind.
        {
            for (int i = 0; i < il.Instrs.Count; i++)
            {
                if (il.Instrs[i].OpCode != OpCodes.Ldc_I4_S)
                    continue;

                if (il.Instrs[i].Operand == null)
                    continue;
                if ((sbyte)il.Instrs[i].Operand != 26) //this will crash if the operand cannot be cast, so make sure the opcode is i4_s so it can be cast.
                    continue;

                il.Instrs[i].Operand = ((sbyte)il.Instrs[i].Operand) + 1; //edit the amount of states to be 26 instead of 25 (or 1 more than what it was previously)
                break;
            }
        }

        private int RewindTime()
        {
            if (thePlayer.CanDash)
            {
                return thePlayer.StartDash();
            }
            //rewinder.Pop();
            thePlayer.Speed = new Vector2(0, 0);
            if (rewinder.Count == 0)
            {
                return 0;
            }
            if (rewinder.Count >= 24)
            {
                RewindStateInfo rewDash = rewinder[rewinder.Count - 24]; //0.4 * 60
                if (rewDash.dash)
                {
                    thePlayer.SceneAs<Level>().Displacement.AddBurst(rewDash.position, 0.4f, 64f, 8f, 0.5f, Ease.QuadIn, Ease.QuadIn);
                    RewindStateInfo slashState = rewinder[rewinder.Count - 23]; //the exact frame does not give the right direction for some reason.
                    ReverseSlashFx.Burst(slashState.position, slashState.dashDir);
                }

            }
            RewindStateInfo rew = rewinder.LastOrDefault();
            rewinder.RemoveAt(rewinder.Count - 1);
            thePlayer.Position = rew.position;
            thePlayer.Sprite.Scale = rew.scale;
            thePlayer.Facing = rew.facing;
            try
            {
                if (rew.animation != "")
                {
                    thePlayer.Sprite.PlayOffset(rew.animation, rew.animFrame);
                    rew.spriteRef.PlayOffset(rew.sweatSprite, rew.sweatFrame);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(rew.animation);
                Console.WriteLine(e.ToString());
            }
            thePlayer.Hair.Nodes = rew.hairNodes; //handle hair physics

            FieldInfo particleField = typeof(ParticleSystem).GetField("particles", BindingFlags.NonPublic | BindingFlags.Instance);

            ParticleSystem fg = thePlayer.SceneAs<Level>().ParticlesFG;
            particleField.SetValue(fg, rew.farticles);

            ParticleSystem part = thePlayer.SceneAs<Level>().Particles;
            particleField.SetValue(part, rew.particles);

            ParticleSystem bg = thePlayer.SceneAs<Level>().ParticlesBG;
            particleField.SetValue(bg, rew.barticles);

            //thePlayer.Speed = -rew.playerSpeed; //could be cool? investigate
            return 26;
        }
        public override void LoadContent(bool firstLoad) {

        }

        public override void Unload() {
            IL.Celeste.Player.ctor -= Player_ctor;
            On.Celeste.Player.ctor -= Player_ctor1;
            On.Celeste.Player.Update -= Player_Update;
            On.Celeste.Player.UpdateSprite -= Player_UpdateSprite;
        }

        public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {

        }


    }
    [CustomEntity("TimeMechanic/TimeEntity")]
    public class TimeEnt : Entity
    {
        DashListener dashListener;
        Sprite sprite;
        int activeTimer;
        bool active = false;

        public TimeEnt(EntityData data, Vector2 offset) : this(data.Position + offset) //idk bro
        {

        }
        public TimeEnt(Vector2 pos) : base(pos)
        {
            base.Depth = -8500;
            base.Collider = new Circle(10f, 0f, 2f);
            base.Add(new PlayerCollider(new Action<Player>(this.OnPlayer), null, null));
            base.Add(this.sprite = new Sprite(GFX.Game, "objects/refill/idle"));

            this.sprite.AddLoop("idle", "", 0.1f);
            this.sprite.Play("idle", false, false);
            this.sprite.CenterOrigin();

            base.Add(this.dashListener = new DashListener());

            this.dashListener.OnDash = new Action<Vector2>(this.OnPlayerDashed);
            Position = pos;
        }
        private void OnPlayer(Player player)
        {
            if(active)
            player.StateMachine.State = 26;
        }
        public void OnPlayerDashed(Vector2 direction)
        {

        }
        public override void Awake(Scene scene)
        {
            base.Awake(scene);
            activeTimer = 0;
        }
        public override void Update()
        {
            base.Update();
            activeTimer++;
            if(activeTimer == 100)
            {
                active = true;
                this.sprite = GFX.SpriteBank.Create("boosterRed");
            }
            Visible = true;
            Console.WriteLine(this.Depth);
            Console.WriteLine(this.Position);
            Console.WriteLine(this.Active);
        }
    }

    public class RewindStateInfo
    {
        public Vector2 position; //TODO: refactor this to use properly cased vars
        public Facings facing;
        public int animFrame;
        public string animation;
        public List<Vector2> hairNodes;
        public int sweatFrame;
        public string sweatSprite;
        public Sprite spriteRef;
        public Vector2 scale;
        public List<BurstStateInfo> burstState;
        public bool dash;
        public float dashDir;
        public Vector2 playerSpeed;

        public Particle[] farticles; //foreground particles
        public Particle[] particles; //ground particles
        public Particle[] barticles; //background particles

        //public PlayerHair Hair;
        //public PlayerSprite Sprite;
        //this shouldnt have this many arguments, set after.
        public RewindStateInfo(Vector2 pos, Facings dir, string animation, int animframe, List<Vector2> hairNodes)//,PlayerHair hair, PlayerSprite sprite)
        {
            position = pos;
            facing = dir;
            this.animation = animation;
            animFrame = animframe;
            this.hairNodes = hairNodes;
        }

        public class BurstStateInfo
        {
            public Vector2 position;
            public float percent;
            public float scaleFrom;
            public float scaleTo;
            public MTexture texture;
            public float duration;
            public float alphaFrom;
            public float alphaTo;
            public Ease.Easer alphaEaser;



            public BurstStateInfo()
            {

            }
        }
    }


}
