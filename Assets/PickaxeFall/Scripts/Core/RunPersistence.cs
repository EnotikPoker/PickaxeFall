using System;
using UnityEngine;

namespace PickaxeFall
{
    public partial class GameController
    {
        GameBalance originalBalance,restoredBalance;
        bool quitting;
        void UseOriginalBalance()
        {
            if(!restoredBalance)return;
            balance=originalBalance;World.SetBalance(balance);Physics2D.gravity=new Vector2(0,balance.gravity);Destroy(restoredBalance);restoredBalance=null;
        }
        public bool AbilityUnlocked(AbilityKind kind)=>kind==AbilityKind.Dash?RunUpgrades.DashUnlocked:kind==AbilityKind.Plunge && RunUpgrades.PlungeUnlocked;
        public bool BuyAbility(AbilityKind kind)
        {
            if(!CanTrade)return false;
            bool success=Economy.TryUnlock(kind);if(success)RunUpgrades=new RunUpgrades(Economy.Profile,balance.progression);
            hud?.Toast(success?"СПОСОБНОСТЬ ОТКРЫТА":Economy.LastError);Notify();return success;
        }
        void GiveResource(BlockKind kind,int count,FallingPickaxe source,Vector2 position)
        {
            if(!Economy.Collect(kind,count))throw new InvalidOperationException(Economy.LastError);
            OreCounts[(int)kind]+=count;dirty=true;
            if(!IsSimulation)hud?.ShowLoot(source!=null?source.Body.position:position,kind,count,source!=null?source.ToolIndex:-1);
        }
        void AwardChest(int x,int row,FallingPickaxe source)
        {
            var random=new System.Random(unchecked(Seed^(x*73856093)^(row*19349663)^0x23bfa71));
            int[] weights={0,25,20,15,10,7,3,8,0,0,1,6,8,5,0,0,0,0,0,0,1};
            int total=0;foreach(var kind in ResourceInventory.Materials)if((int)kind<balance.blocks.Length && weights[(int)kind]>0 && row>=balance.Block(kind).minimumDepth)total+=weights[(int)kind];
            for(int roll=0,n=2+random.Next(3);roll<n;roll++)
            {
                int value=random.Next(total);
                foreach(var kind in ResourceInventory.Materials)
                {
                    if((int)kind>=balance.blocks.Length || weights[(int)kind]<=0 || row<balance.Block(kind).minimumDepth)continue;value-=weights[(int)kind];if(value>=0)continue;
                    int amount=kind==BlockKind.Diamond || kind==BlockKind.Emerald?1:kind==BlockKind.Stone?random.Next(4,13):kind==BlockKind.Coal?random.Next(3,9):random.Next(1,5);
                    GiveResource(kind,amount,source,new Vector2(x,-row-.5f));break;
                }
            }
            hud?.Toast("СУНДУК · РЕСУРСЫ В РЮКЗАКЕ");
        }
        public RunSnapshot CaptureRun()
        {
            if(Phase!=RunPhase.Mining)return null;
            var saved=new RunSnapshot {seed=Seed,ticks=Clock.Ticks,depth=Depth,blocks=BlocksBroken,benches=WorkbenchesUsed,toolCount=RunToolCount,selectedTool=SelectedToolIndex,selectedTier=selectedTier,
                balanceJson=JsonUtility.ToJson(balance),benchVisits=BenchVisits.ToArray(),liquids=World.Liquids.Capture(),rescueUsed=RescueUsed,levels=RunUpgrades.CaptureLevels(),dashUnlocked=RunUpgrades.DashUnlocked,plungeUnlocked=RunUpgrades.PlungeUnlocked,
                energy=Energy.Value,energyEarned=Energy.Earned,loot=(long[])OreCounts.Clone(),rollStates=Rolls.Capture(),attempts=(long[])Rolls.Attempts.Clone(),successes=(long[])Rolls.Successes.Clone(),
                benefits=JsonUtility.FromJson<RunBenefits>(JsonUtility.ToJson(Benefits)),cells=World.CaptureChanges(),tools=new ToolSnapshot[tools.Count]};
            for(int i=0;i<tools.Count;i++){saved.tools[i]=tools[i].Capture();saved.tools[i].index=i;}
            saved.Validate();return saved;
        }
        public void RestoreRun(RunSnapshot saved)
        {
            saved.Validate();StopAllCoroutines();foreach(var tool in tools)tool.Stop();Effects.Clear();UseOriginalBalance();
            restoredBalance=Instantiate(originalBalance);JsonUtility.FromJsonOverwrite(saved.balanceJson,restoredBalance);if(saved.version==1){restoredBalance.rulesVersion=1;restoredBalance.progression=ProgressionBalance.Legacy();}
            restoredBalance.progression.Validate();balance=restoredBalance;World.SetBalance(balance);Physics2D.gravity=new Vector2(0,balance.gravity);
            RunUpgrades=new RunUpgrades(new PlayerProfile {upgrades=(int[])saved.levels.Clone(),dashUnlocked=saved.dashUnlocked,plungeUnlocked=saved.plungeUnlocked},balance.progression);
            BenchVisits.Clear();if(saved.benchVisits!=null)BenchVisits.AddRange(saved.benchVisits);
            Seed=saved.seed;Clock.Restore(saved.ticks);Depth=saved.depth;BlocksBroken=saved.blocks;WorkbenchesUsed=saved.benches;RunToolCount=saved.toolCount;selectedTier=saved.selectedTier;RescueUsed=saved.rescueUsed;
            Array.Copy(saved.loot,OreCounts,OreCounts.Length);Rolls.Restore(saved.rollStates,saved.attempts,saved.successes);Energy.Reset(RunUpgrades);Energy.Restore(saved.energy,saved.energyEarned);
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(saved.benefits),Benefits);World.ResetWorld(Seed,saved.cells,saved.liquids);
            for(int i=0;i<tools.Count;i++)tools[i].Restore(saved.tools[i]);
            Phase=RunPhase.Mining;Paused=true;Time.timeScale=0;SelectTool(saved.selectedTool);World.StreamTools(tools);World.Liquids.RefreshVisuals();Physics2D.SyncTransforms();cameraFollow?.SnapToTools();
            hud?.SelectTab(HubTab.Mine);hud?.CancelGesture();nextSave=Time.unscaledTime+5;Notify();hud?.Toast("СПУСК ВОССТАНОВЛЕН · НАЖМИ «ПРОДОЛЖИТЬ»");
        }
    }
}
