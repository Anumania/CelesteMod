using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.TimeMechanic
{
    public class ReverseSlashFx : SlashFx
    {
        public ReverseSlashFx() : base(){
            this.Sprite.Add("play", "", 0.1f, new int[]
            {
                3,
                2,
                1,
                0
            });
        }
        public static ReverseSlashFx Burst(Vector2 position, float direction)
        {
            Scene scene = Engine.Scene;
            ReverseSlashFx slashFx = Engine.Pooler.Create<ReverseSlashFx>();
            scene.Add(slashFx);
            slashFx.Position = position;
            slashFx.Direction = Calc.AngleToVector(direction, 1f);
            slashFx.Sprite.Play("play", true, false);
            slashFx.Sprite.Scale = Vector2.One;
            slashFx.Sprite.Rotation = 0f;
            if (Math.Abs(direction - 3.14159274f) > 0.01f)
            {
                slashFx.Sprite.Rotation = direction;
            }
            slashFx.Visible = (slashFx.Active = true);
            return slashFx;
        }
    }
}
