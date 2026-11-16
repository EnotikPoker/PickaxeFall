import android.os.SystemClock;
import android.view.InputEvent;
import android.view.InputDevice;
import android.view.MotionEvent;
import java.lang.reflect.Method;

/** Shell-owned touch injection for the isolated PickaxeFall verification APK. */
public final class AndroidTouchProbe {
    static Object manager;
    static Method inject;
    static long down;
    static float x,y;
    static void event(int action,int count) throws Exception {
        MotionEvent.PointerProperties[] p=new MotionEvent.PointerProperties[count];
        MotionEvent.PointerCoords[] c=new MotionEvent.PointerCoords[count];
        for(int i=0;i<count;i++){
            p[i]=new MotionEvent.PointerProperties();p[i].id=i;p[i].toolType=MotionEvent.TOOL_TYPE_FINGER;
            c[i]=new MotionEvent.PointerCoords();c[i].x=x+i*24;c[i].y=y;c[i].pressure=1;c[i].size=.1f;
        }
        MotionEvent e=MotionEvent.obtain(down,SystemClock.uptimeMillis(),action,count,p,c,0,0,1,1,0,0,InputDevice.SOURCE_TOUCHSCREEN,0);
        try{if(!((Boolean)inject.invoke(manager,e,2)))throw new IllegalStateException("Touch injection rejected");}finally{e.recycle();}
        SystemClock.sleep(35);
    }
    public static void main(String[] args) throws Exception {
        x=Float.parseFloat(args[1]);y=Float.parseFloat(args[2]);
        Class<?> cls;
        try{cls=Class.forName("android.hardware.input.InputManagerGlobal");}catch(ClassNotFoundException e){cls=Class.forName("android.hardware.input.InputManager");}
        manager=cls.getMethod("getInstance").invoke(null);inject=cls.getMethod("injectInputEvent",InputEvent.class,int.class);
        if(args[0].equals("pair"))for(int i=0;i<15;i++){
            down=SystemClock.uptimeMillis();event(MotionEvent.ACTION_DOWN,1);event(MotionEvent.ACTION_POINTER_DOWN|(1<<8),2);
            event(MotionEvent.ACTION_POINTER_UP|(1<<8),2);event(MotionEvent.ACTION_UP,1);
        }
        else if(args[0].equals("hold")){
            down=SystemClock.uptimeMillis();event(MotionEvent.ACTION_DOWN,1);
            for(int i=0;i<15;i++){x+=i%2==0?3:-3;event(MotionEvent.ACTION_MOVE,1);SystemClock.sleep(50);}
            event(MotionEvent.ACTION_UP,1);
        }
        else if(args[0].equals("cancel")){
            down=SystemClock.uptimeMillis();event(MotionEvent.ACTION_DOWN,1);event(MotionEvent.ACTION_CANCEL,1);
            down=SystemClock.uptimeMillis();event(MotionEvent.ACTION_DOWN,1);event(MotionEvent.ACTION_UP,1);
        }
        else if(args[0].equals("edge")){down=SystemClock.uptimeMillis();event(MotionEvent.ACTION_DOWN,1);event(MotionEvent.ACTION_UP,1);}
        else for(int i=0;i<30;i++){down=SystemClock.uptimeMillis();event(MotionEvent.ACTION_DOWN,1);event(MotionEvent.ACTION_UP,1);}
        System.out.println("TOUCH_INJECTION_OK: "+args[0]);
    }
}
