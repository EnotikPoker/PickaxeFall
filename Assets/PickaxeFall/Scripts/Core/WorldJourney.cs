using UnityEngine;
namespace PickaxeFall
{
    public partial class GameController
    {
        public MapId CurrentMap=>balance.map;
        public GameAudio Audio {get;private set;}
        public bool SelectMap(MapId map)
        {
            if(Phase!=RunPhase.Ready && Phase!=RunPhase.Finished || !Economy.TrySelectMap(map))return false;
            NewRun();return true;
        }
        void ApplySelectedMap()
        {
            if(IsSimulation)return;
            restoredBalance=Instantiate(originalBalance);balance=restoredBalance;balance.map=Economy.Profile.selectedMap;World.SetBalance(balance);
        }
        public void TrialEvent(bool failed){if(failed)Benefits.FailedTrials++;Audio?.Play(failed?"error":"trial");hud?.Toast(failed?"СПАВНЕР ПОГАС · БЕЗ ШТРАФА":"СПАВНЕР · 4 УДАРА ЗА 12 СЕКУНД");}
        public void AwardTrial(int x,int row,FallingPickaxe source)=>AwardTreasure(x,row,source,true);
        void AwardTreasure(int x,int row,FallingPickaxe source,bool trial=false)
        {
            if(trial)Benefits.Trials++;else Benefits.Chests++;
            bool temple=!trial && balance.map==MapId.Desert && World.Generator.TryFeature(row/64,out int cx,out int cy,out _) && row==cy+5 && x==cx-2;
            bool extra=!trial && RollEffect(UpgradeKind.TreasureHunter);if(extra)Benefits.TreasureBonuses++;
            var loot=ChestLoot.Roll(balance,Seed,x,row,trial,temple,extra);
            foreach(var item in loot)GiveResource(item.kind,item.count,source,new Vector2(x,-row-.5f),false);
            Audio?.Play(trial?"reward":"chest");hud?.ShowTreasure(loot,trial?"ИСПЫТАНИЕ ПРОЙДЕНО":"СУНДУК ОТКРЫТ");
        }
    }
    public sealed partial class EconomyService
    {
        public bool TrySelectMap(MapId map){if(!System.Enum.IsDefined(typeof(MapId),map)||!WorldCatalog.Open(Profile,map)){LastError="Сначала построй маршрут.";return false;}var p=Profile.Copy();p.selectedMap=map;return Commit(p);}
    }
}
