﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace OneKeyToWin_AIO_Sebby
{
    class MissFortune
    {
        private Menu Config = Program.Config;
        public static Orbwalking.Orbwalker Orbwalker = Program.Orbwalker;
        private Spell E, Q, Q1, R, W;
        private float QMANA = 0, WMANA = 0, EMANA = 0, RMANA = 0;
        public Obj_AI_Hero Player { get { return ObjectManager.Player; } }
        private int LastAttackId = 0;
        private float RCastTime = 0;

        public void LoadOKTW()
        {
            Q = new Spell(SpellSlot.Q, 655f);
            Q1 = new Spell(SpellSlot.Q, 1300f);
            W = new Spell(SpellSlot.W);
            E = new Spell(SpellSlot.E, 1000f);
            R = new Spell(SpellSlot.R, 1350f);

            Q1.SetSkillshot(0.25f, 80f, 1200f, true, SkillshotType.SkillshotLine);
            Q.SetTargetted(0.25f, 1400f);
            E.SetSkillshot(0.5f, 200f, float.MaxValue, false, SkillshotType.SkillshotCircle);
            R.SetSkillshot(0.25f, 100f, 2000f, false, SkillshotType.SkillshotCircle);

            Config.SubMenu(Player.ChampionName).SubMenu("Draw").AddItem(new MenuItem("onlyRdy", "Draw only ready spells", true).SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("Draw").AddItem(new MenuItem("QRange", "Q range", true).SetValue(false));
            Config.SubMenu(Player.ChampionName).SubMenu("Draw").AddItem(new MenuItem("ERange", "E range", true).SetValue(false));
            Config.SubMenu(Player.ChampionName).SubMenu("Draw").AddItem(new MenuItem("RRange", "R range", true).SetValue(false));
            Config.SubMenu(Player.ChampionName).SubMenu("Draw").AddItem(new MenuItem("noti", "Show notification & line", true).SetValue(true));

            Config.SubMenu(Player.ChampionName).SubMenu("Q Config").AddItem(new MenuItem("autoQ", "Auto Q", true).SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("Q Config").AddItem(new MenuItem("harasQ", "Use Q on minion", true).SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("Q Config").AddItem(new MenuItem("killQ", "Use Q only if can kill minion", true).SetValue(false));

            Config.SubMenu(Player.ChampionName).SubMenu("W Config").AddItem(new MenuItem("autoW", "Auto W", true).SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("W Config").AddItem(new MenuItem("harasW", "Harass W", true).SetValue(true));

            Config.SubMenu(Player.ChampionName).SubMenu("E Config").AddItem(new MenuItem("autoE", "Auto E", true).SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("E Config").AddItem(new MenuItem("AGC", "AntiGapcloserE", true).SetValue(true));

            Config.SubMenu(Player.ChampionName).SubMenu("R Config").AddItem(new MenuItem("autoR", "Auto R", true).SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("R Config").AddItem(new MenuItem("forceBlockMove", "Force block player", true).SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("R Config").AddItem(new MenuItem("useR", "Semi-manual cast R key", true).SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Press))); //32 == space
            Config.SubMenu(Player.ChampionName).SubMenu("R Config").AddItem(new MenuItem("disableBlock", "Disable R key", true).SetValue(new KeyBind("R".ToCharArray()[0], KeyBindType.Press))); //32 == space

            Config.SubMenu(Player.ChampionName).AddItem(new MenuItem("newTarget", "Try change focus after attack ", true).SetValue(true));

            Config.SubMenu(Player.ChampionName).SubMenu("Farm").AddItem(new MenuItem("jungleE", "Jungle clear E", true).SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("Farm").AddItem(new MenuItem("jungleQ", "Jungle Q ks", true).SetValue(true));
            Config.SubMenu(Player.ChampionName).SubMenu("Farm").AddItem(new MenuItem("jungleW", "Jungle clear W", true).SetValue(true));


            Game.OnUpdate += Game_OnGameUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            Orbwalking.AfterAttack += afterAttack;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
        }

        private void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (E.IsReady() && Config.Item("AGC", true).GetValue<bool>() &&  Player.Mana > RMANA + EMANA)
            {
                var Target = gapcloser.Sender;
                if (Target.IsValidTarget(E.Range))
                {
                    E.Cast(gapcloser.End);
                }
                return;
            }
            return;
        }

        private void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe && args.SData.Name == "MissFortuneBulletTime")
            {
                RCastTime = Game.Time;
                Program.debug(args.SData.Name);
                Orbwalking.Attack = false;
                Orbwalking.Move = false;
                if (Config.Item("forceBlockMove", true).GetValue<bool>())
                {
                    OktwCommon.blockMove = true;
                    OktwCommon.blockAttack = true;
                    OktwCommon.blockSpells = true;
                }
            }
        }

        private void afterAttack(AttackableUnit unit, AttackableUnit target)
        {
            if (!unit.IsMe)
                return;
            LastAttackId = target.NetworkId;

            if (!(target is Obj_AI_Hero))
                return;
            var t = target as Obj_AI_Hero;

            if (Q.IsReady() && t.IsValidTarget(Q.Range))
            {
                if (Q.GetDamage(t) + Player.GetAutoAttackDamage(t) * 3 > t.Health)
                    Q.Cast(t);
                else if (Program.Combo && Player.Mana > RMANA + QMANA + WMANA)
                    Q.Cast(t);
                else if (Program.Farm && Player.Mana > RMANA + QMANA + EMANA + WMANA)
                    Q.Cast(t);
            }
            if (W.IsReady())
            {
                if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && Player.Mana > RMANA + WMANA && Config.Item("autoW", true).GetValue<bool>())
                    W.Cast();
                else if (Player.Mana > RMANA + WMANA + QMANA && Config.Item("harasW", true).GetValue<bool>())
                    W.Cast();
            }
        }

        private void Jungle()
        {
            if (Program.LaneClear && Player.Mana > RMANA + WMANA + QMANA )
            {
                var mobs = MinionManager.GetMinions(Player.ServerPosition, 600, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);
                if (mobs.Count > 0)
                {
                    var mob = mobs[0];
                    if (Q.IsReady() && Config.Item("jungleQ", true).GetValue<bool>() && Q.GetDamage(mob) > mob.Health)
                    {
                        Q.Cast(mob);
                        return;
                    }
                    if (W.IsReady() && Config.Item("jungleW", true).GetValue<bool>())
                    {
                        W.Cast();
                        return;
                    }
                    if (E.IsReady() && Config.Item("jungleE", true).GetValue<bool>())
                    {
                        E.Cast(mob.ServerPosition);
                        return;
                    }
                }
            }
        }

        private void Game_OnGameUpdate(EventArgs args)
        {
            if (Config.Item("disableBlock", true).GetValue<KeyBind>().Active)
            {
                Orbwalking.Attack = true;
                Orbwalking.Move = true;
                OktwCommon.blockSpells = false;
                OktwCommon.blockAttack = false;
                OktwCommon.blockMove = false;
                return;
            }
            else if (Player.IsChannelingImportantSpell() || Game.Time - RCastTime < 0.3)
            {
                if (Config.Item("forceBlockMove", true).GetValue<bool>())
                {
                    OktwCommon.blockMove = true;
                    OktwCommon.blockAttack = true;
                    OktwCommon.blockSpells = true;
                }

                Orbwalking.Attack = false;
                Orbwalking.Move = false;
               
                Program.debug("cast R");
                return;
            }
            else
            {
                Orbwalking.Attack = true;
                Orbwalking.Move = true;
                if (Config.Item("forceBlockMove", true).GetValue<bool>())
                {
                    OktwCommon.blockAttack = false;
                    OktwCommon.blockMove = false;
                    OktwCommon.blockSpells = false;
                }
                if (R.IsReady() && Config.Item("useR", true).GetValue<KeyBind>().Active)
                {
                    var t = TargetSelector.GetTarget(R.Range, TargetSelector.DamageType.Physical);
                    if (t.IsValidTarget(R.Range))
                    {
                        R.Cast(t, true, true);
                        RCastTime = Game.Time;
                        return;
                    }
                }
            }

            if (Config.Item("newTarget", true).GetValue<bool>())
            {
                var orbT = Orbwalker.GetTarget();

                Obj_AI_Hero t2 = null;

                if (orbT != null && orbT is Obj_AI_Hero)
                    t2 = (Obj_AI_Hero)orbT;

                if (t2.IsValidTarget() && t2.NetworkId == LastAttackId)
                {
                    var ta = ObjectManager.Get<Obj_AI_Hero>().Where(enemy => 
                        enemy.IsValidTarget() && Orbwalking.InAutoAttackRange(enemy) 
                            && (enemy.NetworkId != LastAttackId || enemy.Health < Player.GetAutoAttackDamage(enemy) * 2) ).FirstOrDefault();

                    if (ta!=null)
                        Orbwalker.ForceTarget(ta);
                }
            }

            if (Program.LagFree(1))
            {
                SetMana();
                Jungle();
            }

            if (Program.LagFree(2) && !Player.IsWindingUp && Q.IsReady() && Config.Item("autoQ", true).GetValue<bool>())
                LogicQ();

            if (Program.LagFree(3) && !Player.IsWindingUp && E.IsReady() && Config.Item("autoE", true).GetValue<bool>())
                LogicE();

            if (Program.LagFree(4) && !Player.IsWindingUp && R.IsReady() && Config.Item("autoR", true).GetValue<bool>())
                LogicR();
            
        }
        private void LogicQ()
        {
            var t = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Physical);
            var t1 = TargetSelector.GetTarget(Q1.Range, TargetSelector.DamageType.Physical);
            if (t.IsValidTarget(Q.Range) && Player.Distance(t.ServerPosition) > 500)
            {
                var qDmg = OktwCommon.GetKsDamage(t, Q);
                if (qDmg + Player.GetAutoAttackDamage(t) > t.Health)
                    Q.Cast(t);
                else if (qDmg + Player.GetAutoAttackDamage(t) * 3 > t.Health)
                    Q.Cast(t);
                else if (Program.Combo && Player.Mana > RMANA + QMANA + WMANA)
                    Q.Cast(t);
                else if (Program.Farm && Player.Mana > RMANA + QMANA + EMANA + WMANA)
                    Q.Cast(t);
            }
            else if (t1.IsValidTarget(Q1.Range) && Config.Item("harasQ", true).GetValue<bool>() && Player.Distance(t1.ServerPosition) > Q.Range + 50)
            {
                var minions = MinionManager.GetMinions(Player.ServerPosition, Q1.Range);

                if (minions.Exists(x => x.IsMoving))
                    return;

                var poutput = Q1.GetPrediction(t1);
                var col = poutput.CollisionObjects;
                if (col.Count() == 0)
                    return;

                var minionQ = col.Last();
                if (minionQ.IsValidTarget(Q.Range))
                {
                    if (Config.Item("killQ", true).GetValue<bool>() && Q.GetDamage(minionQ) < minionQ.Health)
                        return;
                    var minionToT = minionQ.Distance(t1.Position);
                    var minionToP = minionQ.Distance(poutput.CastPosition);
                    if (minionToP < 400 && minionToT < 420 && minionToT > 150 && minionToP > 200)
                    {
                        if (Q.GetDamage(t1) + Player.GetAutoAttackDamage(t1) > t1.Health)
                            Q.Cast(col.Last());
                        else if (Program.Combo && Player.Mana > RMANA + QMANA + WMANA)
                            Q.Cast(col.Last());
                        else if (Program.Farm && Player.Mana > RMANA + QMANA + EMANA + WMANA + QMANA)
                            Q.Cast(col.Last());
                    }
                }
            }
        }
        private void LogicE()
        {
            var t = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Magical);
            if (t.IsValidTarget())
            {
                if (Program.GetRealDmg(E, t) > t.Health)
                    Program.CastSpell(E, t);
                else if (E.GetDamage(t) + Q.GetDamage(t) > t.Health && Player.Mana > QMANA + EMANA + RMANA)
                    Program.CastSpell(E, t);
                else if (Program.Combo && Player.Mana > RMANA + WMANA + QMANA + EMANA)
                {
                    if (!Orbwalking.InAutoAttackRange(t) || Player.CountEnemiesInRange(300) > 0 || t.CountEnemiesInRange(250) > 1)
                        Program.CastSpell(E, t);
                    else 
                    {
                        foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValidTarget(E.Range) && !OktwCommon.CanMove(enemy)))
                            E.Cast(enemy, true, true);
                    }
                }
            }   
        }

        private void LogicR()
        {
            var t = TargetSelector.GetTarget(R.Range, TargetSelector.DamageType.Physical);

            if (t.IsValidTarget(R.Range) && OktwCommon.ValidUlt(t))
            {
                var rDmg = R.GetDamage(t) * new double[] { 0.5, 0.75, 1 }[R.Level];

                if (Player.CountEnemiesInRange(700) == 0 && t.CountAlliesInRange(400) == 0)
                {
                    var tDis = Player.Distance(t.ServerPosition);
                    if (rDmg * 7 > t.Health && tDis < 800)
                    {
                        R.Cast(t, true, true);
                        RCastTime = Game.Time;
                    }
                    else if (rDmg * 6 > t.Health && tDis < 900)
                    {
                        R.Cast(t, true, true);
                        RCastTime = Game.Time;
                    }
                    else if (rDmg * 5 > t.Health && tDis < 1000)
                    {
                        R.Cast(t, true, true);
                        RCastTime = Game.Time;
                    }
                    else if (rDmg * 4 > t.Health && tDis < 1100)
                    {
                        R.Cast(t, true, true);
                        RCastTime = Game.Time;
                    }
                    else if (rDmg * 3 > t.Health && tDis < 1200)
                    {
                        R.Cast(t, true, true);
                        RCastTime = Game.Time;
                    }
                    else if (rDmg > t.Health && tDis < 1300)
                    {
                        R.Cast(t, true, true);
                        RCastTime = Game.Time;
                    }
                    return;
                }
                if (rDmg * 8 > t.Health - OktwCommon.GetIncomingDamage(t) && rDmg * 2 < t.Health && Player.CountEnemiesInRange(300) == 0 && !OktwCommon.CanMove(t))
                {
                    R.Cast(t, true, true);
                    RCastTime = Game.Time;
                    return;
                }
            }

        }

        private void SetMana()
        {
            if ((Config.Item("manaDisable", true).GetValue<bool>() && Program.Combo) || Player.HealthPercent < 20)
            {
                QMANA = 0;
                WMANA = 0;
                EMANA = 0;
                RMANA = 0;
                return;
            }

            QMANA = Q.Instance.ManaCost;
            WMANA = W.Instance.ManaCost;
            EMANA = E.Instance.ManaCost;

            if (!R.IsReady())
                RMANA = QMANA - Player.PARRegenRate * Q.Instance.Cooldown;
            else
                RMANA = R.Instance.ManaCost;
        }

        public static void drawLine(Vector3 pos1, Vector3 pos2, int bold, System.Drawing.Color color)
        {
            var wts1 = Drawing.WorldToScreen(pos1);
            var wts2 = Drawing.WorldToScreen(pos2);

            Drawing.DrawLine(wts1[0], wts1[1], wts2[0], wts2[1], bold, color);
        }

        private void Drawing_OnDraw(EventArgs args)
        {
            if (Config.Item("noti", true).GetValue<bool>() && R.IsReady())
            {
                var t = TargetSelector.GetTarget(R.Range, TargetSelector.DamageType.Physical);

                if (t.IsValidTarget())
                {
                    var rDamage = R.GetDamage(t) + (W.GetDamage(t) * 10);
                    if (rDamage * 8 > t.Health)
                    {
                        Drawing.DrawText(Drawing.Width * 0.1f, Drawing.Height * 0.5f, System.Drawing.Color.GreenYellow, "8 x R wave can kill: " + t.ChampionName + " have: " + t.Health + "hp");
                        drawLine(t.Position, Player.Position, 10, System.Drawing.Color.GreenYellow);
                    }
                    else if (rDamage * 5 > t.Health)
                    {
                        Drawing.DrawText(Drawing.Width * 0.1f, Drawing.Height * 0.5f, System.Drawing.Color.Orange, "5 x R wave can kill: " + t.ChampionName + " have: " + t.Health + "hp");
                        drawLine(t.Position, Player.Position, 10, System.Drawing.Color.Orange);
                    }
                    else if (rDamage * 3 > t.Health)
                    {
                        Drawing.DrawText(Drawing.Width * 0.1f, Drawing.Height * 0.5f, System.Drawing.Color.Yellow, "3 x R wave can kill: " + t.ChampionName + " have: " + t.Health + "hp");
                        drawLine(t.Position, Player.Position, 10, System.Drawing.Color.Yellow);
                    }
                    else if (rDamage > t.Health)
                    {
                        Drawing.DrawText(Drawing.Width * 0.1f, Drawing.Height * 0.5f, System.Drawing.Color.Red, "1 x R wave can kill: " + t.ChampionName + " have: " + t.Health + "hp");
                        drawLine(t.Position, Player.Position, 10, System.Drawing.Color.Red);
                    }
                }
            }

            if (Config.Item("QRange", true).GetValue<bool>())
            {
                if (Config.Item("onlyRdy", true).GetValue<bool>())
                {
                    if (W.IsReady())
                        Utility.DrawCircle(Player.Position, Q.Range, System.Drawing.Color.Cyan, 1, 1);
                }
                else
                    Utility.DrawCircle(Player.Position, Q.Range, System.Drawing.Color.Cyan, 1, 1);
            }
            if (Config.Item("ERange", true).GetValue<bool>())
            {
                if (Config.Item("onlyRdy", true).GetValue<bool>())
                {
                    if (E.IsReady())
                        Utility.DrawCircle(Player.Position, E.Range, System.Drawing.Color.Orange, 1, 1);
                }
                else
                    Utility.DrawCircle(Player.Position, E.Range, System.Drawing.Color.Orange, 1, 1);
            }
            if (Config.Item("RRange", true).GetValue<bool>())
            {
                if (Config.Item("onlyRdy", true).GetValue<bool>())
                {
                    if (R.IsReady())
                        Utility.DrawCircle(Player.Position, R.Range, System.Drawing.Color.Gray, 1, 1);
                }
                else
                    Utility.DrawCircle(Player.Position, R.Range, System.Drawing.Color.Gray, 1, 1);
            }
        }

        public static void drawText(string msg, Obj_AI_Base Hero, System.Drawing.Color color)
        {
            var wts = Drawing.WorldToScreen(Hero.Position);
            Drawing.DrawText(wts[0] - (msg.Length) * 5, wts[1], color, msg);

        }
    }
}