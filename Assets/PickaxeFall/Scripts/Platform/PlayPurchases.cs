using System;
using System.IO;
using UnityEngine;
#if PICKAXEFALL_GOOGLE_PLAY && UNITY_ANDROID && !UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;
#endif
namespace PickaxeFall
{
    // Permanent entitlement is separate from game progress and never imported from cloud.
    public sealed class PlayPurchases:IGamePurchases,IDisposable
    {
        readonly PlayReleaseConfig config;readonly string path;bool started,connected,disposed,productsReady;
        public bool RemoveAds {get;private set;}
        public bool Ready {get;private set;}
        public bool Busy {get;private set;}
        public string Price {get;private set;}="";
        public string Status {get;private set;}="Магазин пока недоступен";
        public event Action Changed;
        [Serializable] sealed class CachedReceipt{public string receipt="";}
#if PICKAXEFALL_GOOGLE_PLAY && UNITY_ANDROID && !UNITY_EDITOR
        StoreController controller;CrossPlatformValidator validator;
#endif
        public PlayPurchases(PlayReleaseConfig config,string path)
        {
            this.config=config;this.path=path;
#if PICKAXEFALL_GOOGLE_PLAY && UNITY_ANDROID && !UNITY_EDITOR
            if(string.IsNullOrEmpty(config.googleBillingPublicKey))return;
            try
            {
                validator=new CrossPlatformValidator(Convert.FromBase64String(config.googleBillingPublicKey),Application.identifier);
                var text=AtomicPlayFile.Read(path,s=>{var r=JsonUtility.FromJson<CachedReceipt>(s);if(r==null||r.receipt==null||!string.IsNullOrEmpty(r.receipt)&&!Valid(r.receipt))throw new InvalidDataException();});
                if(text!=null)RemoveAds=Valid(JsonUtility.FromJson<CachedReceipt>(text).receipt);
            }catch(Exception){Status="Не удалось проверить сохранённую покупку";}
#endif
        }
        void Signal(){if(!disposed)Changed?.Invoke();}
        public void Start()
        {
            if(started||disposed)return;started=true;
#if PICKAXEFALL_GOOGLE_PLAY && UNITY_ANDROID && !UNITY_EDITOR
            if(validator==null){Status="Покупки подключатся после настройки Google Play";return;}
            Connect();
#endif
        }
        public void Tick(){}
        public void Buy()
        {
            if(!Ready||Busy||RemoveAds)return;
#if PICKAXEFALL_GOOGLE_PLAY && UNITY_ANDROID && !UNITY_EDITOR
            Busy=true;Status="Открываем Google Play…";Signal();
            try{controller.PurchaseProduct(PlayReleaseConfig.Product);}catch(Exception){Busy=false;Status="Не удалось открыть покупку";Signal();}
#endif
        }
        public void Restore()
        {
            if(Busy||disposed)return;
#if PICKAXEFALL_GOOGLE_PLAY && UNITY_ANDROID && !UNITY_EDITOR
            if(!connected){started=false;Start();return;}Status="Проверяем покупки…";controller.FetchPurchases();
#endif
        }
#if PICKAXEFALL_GOOGLE_PLAY && UNITY_ANDROID && !UNITY_EDITOR
        async void Connect()
        {
            try
            {
                if(controller!=null)Unsubscribe();controller=UnityIAPServices.StoreController();
                controller.OnStoreConnected+=Connected;controller.OnStoreDisconnected+=Disconnected;
                controller.OnProductsFetched+=Products;controller.OnProductsFetchFailed+=ProductsFailed;
                controller.OnPurchasePending+=Pending;controller.OnPurchaseConfirmed+=Confirmed;controller.OnPurchaseFailed+=Failed;controller.OnPurchaseDeferred+=Deferred;
                controller.OnPurchasesFetched+=Fetched;controller.OnPurchasesFetchFailed+=FetchFailed;
                await controller.Connect();
            }
            catch(Exception){Ready=false;Status="Магазин недоступен. Можно играть без подключения.";Signal();}
        }
        void Connected(){if(disposed)return;connected=true;controller.FetchProducts(new List<ProductDefinition>{new(PlayReleaseConfig.Product,ProductType.NonConsumable)});}
        void Disconnected(StoreConnectionFailureDescription value){connected=false;Ready=false;Busy=false;Status="Нет связи с Google Play";Signal();}
        void Products(List<Product> products)
        {
            foreach(var product in products)if(product.definition.id==PlayReleaseConfig.Product&&product.availableToPurchase)
            {Price=product.metadata.localizedPriceString;productsReady=true;}
            controller.FetchPurchases();Signal();
        }
        void ProductsFailed(ProductFetchFailed value){Ready=false;Status="Не удалось загрузить цену";Signal();}
#if PICKAXEFALL_PLAY_QA && DEVELOPMENT_BUILD
        public bool QaValidate(string receipt){try{return Valid(receipt);}catch(Exception){return false;}}
#endif
        bool Valid(string receipt)
        {
            if(string.IsNullOrEmpty(receipt)||validator==null)return false;
            foreach(var r in validator.Validate(receipt))if(r is GooglePlayReceipt google&&google.productID==PlayReleaseConfig.Product&&google.packageName==Application.identifier&&google.purchaseState==GooglePurchaseState.Purchased&&!string.IsNullOrEmpty(google.purchaseToken))return true;
            return false;
        }
        bool Grant(Order order)
        {
            try
            {
                if(!Valid(order.Info.Receipt)){Status="Не удалось проверить покупку";return false;}
                Cache(order.Info.Receipt);RemoveAds=true;Status="Принудительная реклама отключена";return true;
            }
            catch(Exception){Status="Не удалось сохранить покупку. Повторим при подключении к магазину.";return false;}
        }
        void Cache(string receipt)=>AtomicPlayFile.Write(path,JsonUtility.ToJson(new CachedReceipt{receipt=receipt}),s=>{var r=JsonUtility.FromJson<CachedReceipt>(s);if(!string.IsNullOrEmpty(r.receipt)&&!Valid(r.receipt))throw new InvalidDataException();},true);
        void Pending(PendingOrder order)
        {
            if(Grant(order))controller.ConfirmPurchase(order);Busy=false;Signal();
        }
        void Confirmed(Order order){Busy=false;Signal();}
        void Failed(FailedOrder order){Busy=false;Status="Покупка не завершена. Деньги и прогресс проверяет Google Play.";Signal();}
        void Deferred(DeferredOrder order){Busy=false;Status="Платёж ожидает подтверждения. Реклама пока не отключена.";Signal();}
        void Fetched(Orders orders)
        {
            Ready=productsReady;bool found=false;
            foreach(var order in orders.ConfirmedOrders){try{if(Valid(order.Info.Receipt)){found=true;Grant(order);}}catch(Exception){}}
            foreach(var order in orders.PendingOrders){try{if(Valid(order.Info.Receipt)){found=true;if(Grant(order))controller.ConfirmPurchase(order);}}catch(Exception){}}
            // Only a successful authoritative store query may revoke an offline entitlement.
            if(!found){try{Cache("");RemoveAds=false;Status="Покупка отключения рекламы не найдена";}catch(Exception){Status="Не удалось обновить сведения о покупке";}}
            Busy=false;Signal();
        }
        void FetchFailed(PurchasesFetchFailureDescription value){Ready=false;Status="Не удалось проверить покупки. Сохранённое право осталось на устройстве.";Busy=false;Signal();}
        void Unsubscribe()
        {
            controller.OnStoreConnected-=Connected;controller.OnStoreDisconnected-=Disconnected;controller.OnProductsFetched-=Products;controller.OnProductsFetchFailed-=ProductsFailed;
            controller.OnPurchasePending-=Pending;controller.OnPurchaseConfirmed-=Confirmed;controller.OnPurchaseFailed-=Failed;controller.OnPurchaseDeferred-=Deferred;
            controller.OnPurchasesFetched-=Fetched;controller.OnPurchasesFetchFailed-=FetchFailed;
        }
#endif
        public void Dispose(){disposed=true;
#if PICKAXEFALL_GOOGLE_PLAY && UNITY_ANDROID && !UNITY_EDITOR
            if(controller!=null)Unsubscribe();
#endif
        }
    }
}
