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

namespace Celeste.Mod.TimeMechanic {
    public class TimeMechanic : EverestModule {
        List<RewindStateInfo> rewinder = new List<RewindStateInfo>();
        public static Player thePlayer;
        public override void LoadSettings() {

        }

        public override void Load() {
            rewinder = new List<RewindStateInfo>();
            IL.Celeste.Player.ctor += Player_ctor;
            On.Celeste.Player.ctor += Player_ctor1;
            On.Celeste.Player.Update += Player_Update;
            On.Celeste.Player.UpdateSprite += Player_UpdateSprite;
        }

        private void Player_UpdateSprite(On.Celeste.Player.orig_UpdateSprite orig, Player self)
        {
            if(thePlayer.StateMachine.State != 26)
            {
                orig(self);
            }
        }

        private void Player_Update(On.Celeste.Player.orig_Update orig, Player self)
        {
            if (thePlayer.StateMachine.State != 26)
            {
                if(rewinder.Count >= 5 * 60)
                {
                    rewinder.RemoveAt(0);
                }
                rewinder.Add(new RewindStateInfo(self.Position, self.Facing, self.Dashes, self.Sprite.CurrentAnimationID, self.Sprite.CurrentAnimationFrame));
                self.Sprite.Rate = -1;
            }
            orig(self);
            if (Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.N))
            {
                self.StateMachine.State = 26;
                TrailManager.Add(thePlayer, thePlayer.GetCurrentTrailColor(),duration:5);
            }
            if (Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.M))
            {
                thePlayer.SceneAs<Level>().Add(new TimeEnt(self.Position));
            }
        }

        private void Player_ctor1(On.Celeste.Player.orig_ctor orig, Player self, Vector2 position, PlayerSpriteMode spriteMode)
        {
            orig(self, position, spriteMode);
            thePlayer = self;
            self.StateMachine.SetCallbacks(26, new Func<int>(this.RewindTime));
        }

        private void Player_ctor(MonoMod.Cil.ILContext il)
        {
            il.Instrs[176].Operand = ((System.SByte)il.Instrs[176].Operand) + 1;
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
            RewindStateInfo rew = rewinder.LastOrDefault();
            rewinder.RemoveAt(rewinder.Count-1);
            thePlayer.Position = rew.Position;
            thePlayer.Facing = rew.Facing;
            thePlayer.Dashes = rew.Dashes;
            try
            {
                if(rew.Animation != "")
                {
                    thePlayer.Sprite.PlayOffset(rew.Animation, rew.Animframe);
                    thePlayer.Sprite.Rate = 0;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(rew.Animation);
                Console.WriteLine(e.ToString());
            }
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
    public class TimeEnt : Entity
    {
        DashListener dashListener;
        Sprite inactiveSprite;
        Sprite sprite;
        int activeTimer;
        bool active = false;
        public TimeEnt(Vector2 pos) : base(pos)
        {
            base.Depth = -8500;
            base.Collider = new Circle(10f, 0f, 2f);
            base.Add(new PlayerCollider(new Action<Player>(this.OnPlayer), null, null));
            base.Add(this.sprite = GFX.SpriteBank.Create("booster"));
            base.Add(this.dashListener = new DashListener());

            this.dashListener.OnDash = new Action<Vector2>(this.OnPlayerDashed);
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
        }
    }

    public class RewindStateInfo
    {
        public Vector2 Position;
        public Facings Facing;
        public int Dashes;
        public int Animframe;
        public string Animation;
        //public PlayerHair Hair;
        //public PlayerSprite Sprite;
        public RewindStateInfo(Vector2 pos, Facings dir, int dashes, string animation, int animframe)//,PlayerHair hair, PlayerSprite sprite)
        {
            Position = pos;
            Facing = dir;
            Dashes = dashes;
            Animation = animation;
            Animframe = animframe;
        }
    }


}
