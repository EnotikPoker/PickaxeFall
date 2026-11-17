mergeInto(LibraryManager.library, {
 PF_Init:function(target){window.PickaxePlatform.init(UTF8ToString(target),function(object,method,value){SendMessage(object,method,value);});},
 PF_LocalRead:function(user){var text=window.PickaxePlatform.read(UTF8ToString(user));var size=lengthBytesUTF8(text)+1,ptr=_malloc(size);stringToUTF8(text,ptr,size);return ptr;},
 PF_LocalWrite:function(user,json,reset){return window.PickaxePlatform.write(UTF8ToString(user),UTF8ToString(json),!!reset);},
 PF_Backup:function(local,cloud){return window.PickaxePlatform.backup(UTF8ToString(local),UTF8ToString(cloud));},
 PF_Ready:function(){window.PickaxePlatform.ready();},
 PF_Gameplay:function(running){window.PickaxePlatform.gameplay(!!running);},
 PF_Login:function(){window.PickaxePlatform.login();},
 PF_Ad:function(id,rewarded){window.PickaxePlatform.ad(UTF8ToString(id),!!rewarded);},
 PF_Cloud:function(json,expected){window.PickaxePlatform.cloud(UTF8ToString(json),UTF8ToString(expected));},
 PF_CloudRead:function(){window.PickaxePlatform.readCloud();},
 PF_Reload:function(){window.PickaxePlatform.reload();}
});
mergeInto(LibraryManager.library, {PF_Probe:function(json){window.pickaxeProbe=JSON.parse(UTF8ToString(json));}});
