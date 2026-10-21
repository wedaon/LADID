using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class LADIDGame : MonoBehaviour
{
    const float W = 8f, H = 5f, PhaseSeconds = 22f;
    enum World { Volcano, Tidal }
    Transform player;
    Transform hazardsRoot;
    SpriteRenderer playerRenderer;
    Color playerColor = new Color(.88f,.96f,1f);
    Camera cam;
    Text worldText, timerText, livesText, messageText;
    Image banner, flash;
    World world;
    float phaseTime, survived, flashTime, damageTintTime, health = 100f;
    bool ended, changing;

    void Start()
    {
        Application.targetFrameRate = 60;
        hazardsRoot = new GameObject("Dimension Hazards").transform;
        cam = new GameObject("Camera").AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 6.5f; cam.transform.position = new Vector3(0,0,-10);
        cam.backgroundColor = new Color(.12f,.09f,.1f);
        Rect("Field", Vector3.zero, 16, 10, new Color(.2f,.19f,.19f), 0);
        for (int i=-7;i<=7;i++) Rect("Grid",new Vector3(i,0,.1f),.018f,10,new Color(1,1,1,.04f),1);
        for (int i=-4;i<=4;i++) Rect("Grid",new Vector3(0,i,.1f),16,.018f,new Color(1,1,1,.04f),1);
        Rect("Top edge",new Vector3(0,5.1f),16.3f,.2f,new Color(.9f,.3f,.16f),1);
        Rect("Bottom edge",new Vector3(0,-5.1f),16.3f,.2f,new Color(.9f,.3f,.16f),1);
        Rect("Left edge",new Vector3(-8.1f,0),.2f,10.3f,new Color(.9f,.3f,.16f),1);
        Rect("Right edge",new Vector3(8.1f,0),.2f,10.3f,new Color(.9f,.3f,.16f),1);
        player = Rect("Player",Vector3.zero,.52f,.52f,playerColor,5).transform;
        playerRenderer = player.GetComponent<SpriteRenderer>();
        BuildHud();
        SetWorld(World.Volcano,true);
        StartCoroutine(PhaseLoop()); StartCoroutine(VolcanoLoop()); StartCoroutine(TideLoop());
    }

    void Update()
    {
        if (ended) { if (Input.GetKeyDown(KeyCode.R)) Restart(); return; }
        float dt=Time.deltaTime; survived+=dt; phaseTime-=dt;
        Vector2 v=new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));
        if(v.sqrMagnitude>1) v.Normalize();
        Vector3 p=player.position+(Vector3)(v*7f*dt); p.x=Mathf.Clamp(p.x,-7.65f,7.65f); p.y=Mathf.Clamp(p.y,-4.65f,4.65f); player.position=p;
        timerText.text="NEXT COLLAPSE  "+Mathf.CeilToInt(phaseTime).ToString("00")+"s";
        ApplyCoreContactDamage(dt);
        livesText.text="HEALTH  "+Mathf.CeilToInt(health).ToString("000")+" / 100";
        if(damageTintTime>0){damageTintTime-=dt;playerRenderer.color=new Color(1f,.16f,.16f);}
        else playerRenderer.color=playerColor;
        if(flashTime>0){flashTime-=dt;flash.color=new Color(1,.15f,.1f,Mathf.Clamp01(flashTime*.35f));}
    }

    void BuildHud()
    {
        var c=new GameObject("HUD").AddComponent<Canvas>(); c.renderMode=RenderMode.ScreenSpaceOverlay; c.sortingOrder=20;
        var scaler=c.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1280,720); c.gameObject.AddComponent<GraphicRaycaster>();
        Label(c.transform,"LADID  /  LIFE AND DEATH IN DIMENSION",24,TextAnchor.UpperLeft,new Vector2(0,1),new Vector2(0,1),new Vector2(28,-22),new Vector2(760,40),Color.white);
        worldText=Label(c.transform,"",20,TextAnchor.UpperRight,new Vector2(1,1),new Vector2(1,1),new Vector2(-28,-24),new Vector2(400,36),Color.white);
        timerText=Label(c.transform,"",16,TextAnchor.UpperRight,new Vector2(1,1),new Vector2(1,1),new Vector2(-28,-60),new Vector2(400,28),new Color(1,.75f,.45f));
        livesText=Label(c.transform,"",20,TextAnchor.UpperLeft,new Vector2(0,1),new Vector2(0,1),new Vector2(28,-72),new Vector2(300,30),Color.white);
        messageText=Label(c.transform,"",16,TextAnchor.MiddleCenter,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,22),new Vector2(900,44),Color.white);
        banner=Panel(c.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(560,100),new Color(.4f,.1f,.07f,.94f));
        Label(banner.transform,"",26,TextAnchor.MiddleCenter,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Color.white);
        flash=Panel(c.transform,Vector2.zero,Vector2.zero,Vector2.zero,new Color(1,.1f,.1f,0));
        flash.rectTransform.anchorMax=Vector2.one; flash.rectTransform.offsetMin=Vector2.zero; flash.rectTransform.offsetMax=Vector2.zero;
        banner.gameObject.SetActive(false); flash.transform.SetAsLastSibling();
    }

    IEnumerator PhaseLoop()
    {
        while(!ended){yield return new WaitForSeconds(PhaseSeconds); if(ended)yield break; changing=true; ClearHazards(); banner.gameObject.SetActive(true); banner.GetComponentInChildren<Text>().text="DIMENSION COLLAPSE"; yield return new WaitForSeconds(1.5f); SetWorld(world==World.Volcano?World.Tidal:World.Volcano,false); changing=false;}
    }
    void SetWorld(World next,bool first)
    {
        if(!first) ClearHazards();
        world=next; phaseTime=PhaseSeconds; cam.backgroundColor=next==World.Volcano?new Color(.12f,.09f,.1f):new Color(.04f,.19f,.27f);
        worldText.text=next==World.Volcano?"01  /  VOLCANIC WASTELAND":"02  /  TIDAL WORLD";
        messageText.text=next==World.Volcano?"VOLCANIC WORLD  •  MOVE OUT OF THE RED WARNING CIRCLES":"TIDAL WORLD  •  EVADE THE HORIZONTAL WAVE BY MOVING UP OR DOWN";
        if(!first){player.position=Vector3.zero; banner.GetComponentInChildren<Text>().text=next==World.Volcano?"ENTERING: VOLCANIC WORLD":"ENTERING: TIDAL WORLD"; banner.color=next==World.Volcano?new Color(.45f,.13f,.06f,.94f):new Color(.02f,.25f,.4f,.94f); StartCoroutine(HideBanner());}
        else banner.gameObject.SetActive(false);
    }
    IEnumerator HideBanner(){yield return new WaitForSeconds(1.1f);if(!changing)banner.gameObject.SetActive(false);}
    IEnumerator VolcanoLoop(){while(true){yield return new WaitForSeconds(Random.Range(.65f,1.2f));if(ended||changing||world!=World.Volcano)continue;StartCoroutine(Meteor(new Vector2(Random.Range(-7.3f,7.3f),Random.Range(-4.3f,4.3f))));}}
    IEnumerator Meteor(Vector2 at)
    {
        GameObject warn=Disc("Impact warning",at,2.4f,new Color(1,.12f,.04f,.4f),2);
        float t=0;while(t<1.05f){if(ended||changing||world!=World.Volcano){Destroy(warn);yield break;}t+=Time.deltaTime;yield return null;}
        Destroy(warn);GameObject hit=Disc("Meteor impact",at,2.4f,new Color(1,.28f,.06f),3);GameObject core=Disc("Impact core",at,1.2f,new Color(1,.78f,.25f),4);StartCoroutine(ShrinkCore(core,10f));
        if(Vector2.Distance(player.position,at)<1.45f)TakeDamage(30f);yield return new WaitForSeconds(.28f);Destroy(hit);
    }
    IEnumerator ShrinkCore(GameObject core,float duration)
    {
        float elapsed=0f;float initialSize=core.transform.localScale.x;
        while(elapsed<duration)
        {
            if(!core)yield break;
            elapsed+=Time.deltaTime;
            float scale=Mathf.Lerp(initialSize,.3f,elapsed/duration);
            core.transform.localScale=Vector3.one*scale;
            yield return null;
        }
        if(core)Destroy(core);
    }
    IEnumerator TideLoop(){while(true){yield return new WaitForSeconds(Random.Range(3f,4.2f));if(ended||changing||world!=World.Tidal)continue;StartCoroutine(Wave(Random.Range(-2.6f,2.6f)));}}
    IEnumerator Wave(float y)
    {
        float band=Random.Range(1.05f,1.55f);bool left=Random.value>.5f;float x=left?-10:10,end=left?10:-10;
        var wave=Rect("Tsunami",new Vector3(x,y,-.3f),2,band*2,new Color(.12f,.75f,.95f,.72f),3);
        wave.transform.SetParent(hazardsRoot,true);
        banner.color=new Color(.02f,.26f,.4f,.92f);banner.GetComponentInChildren<Text>().text=left?"WAVE INCOMING  →":"←  WAVE INCOMING";banner.gameObject.SetActive(true);yield return new WaitForSeconds(1.05f);
        if(ended||changing||world!=World.Tidal){Destroy(wave);yield break;}banner.gameObject.SetActive(false);
        bool hit=false;while(!ended&&!changing&&world==World.Tidal&&(left?wave.transform.position.x<end:wave.transform.position.x>end))
        {
            float dt=Time.deltaTime;var p=wave.transform.position;p.x+=(left?16:-16)*dt;wave.transform.position=p;
            bool touching=Mathf.Abs(player.position.y-y)<band+.34f&&Mathf.Abs(player.position.x-p.x)<1.25f;
            if(touching&&!hit){hit=true;TakeDamage(40f);player.position=new Vector3(player.position.x,Mathf.Clamp(y+(player.position.y<y?-1:1)*(band+.7f),-4.6f,4.6f),0);}
            yield return null;
        }Destroy(wave);
    }
    void ApplyCoreContactDamage(float dt)
    {
        for(int i=0;i<hazardsRoot.childCount;i++)
        {
            Transform hazard=hazardsRoot.GetChild(i);
            float contactRadius=(hazard.localScale.x+.52f)*.5f;
            if(hazard.name=="Impact core"&&Vector2.Distance(player.position,hazard.position)<contactRadius)TakeDamage(10f*dt);
        }
    }
    void TakeDamage(float amount){if(ended)return;health=Mathf.Max(0,health-amount);flashTime=.2f;damageTintTime=.18f;if(health<=0){ended=true;messageText.text="LOST BETWEEN DIMENSIONS  •  PRESS R TO RESTART";banner.GetComponentInChildren<Text>().text="DIMENSION LOST";banner.gameObject.SetActive(true);}}
    void ClearHazards(){for(int i=hazardsRoot.childCount-1;i>=0;i--)Destroy(hazardsRoot.GetChild(i).gameObject);}
    void Restart(){StopAllCoroutines();ClearHazards();health=100f;survived=0;ended=false;changing=false;flashTime=0;damageTintTime=0;playerRenderer.color=playerColor;player.position=Vector3.zero;SetWorld(World.Volcano,true);StartCoroutine(PhaseLoop());StartCoroutine(VolcanoLoop());StartCoroutine(TideLoop());}
    GameObject Rect(string n,Vector3 p,float w,float h,Color c,int order){var o=new GameObject(n);o.transform.position=p;o.transform.localScale=new Vector3(w,h,1);var r=o.AddComponent<SpriteRenderer>();r.sprite=Sprites.Square;r.color=c;r.sortingOrder=order;return o;}
    GameObject Disc(string n,Vector2 p,float s,Color c,int order){var o=new GameObject(n);o.transform.SetParent(hazardsRoot,false);o.transform.position=p;o.transform.localScale=Vector3.one*s;var r=o.AddComponent<SpriteRenderer>();r.sprite=Sprites.Circle;r.color=c;r.sortingOrder=order;return o;}
    static Text Label(Transform root,string value,int size,TextAnchor align,Vector2 amin,Vector2 amax,Vector2 pos,Vector2 dim,Color color){var o=new GameObject("Label");o.transform.SetParent(root,false);var r=o.AddComponent<RectTransform>();r.anchorMin=amin;r.anchorMax=amax;r.pivot=new Vector2(amin.x==amax.x?amin.x:.5f,amin.y==amax.y?amin.y:.5f);r.anchoredPosition=pos;r.sizeDelta=dim;var t=o.AddComponent<Text>();t.text=value;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.fontStyle=FontStyle.Bold;t.alignment=align;t.color=color;t.horizontalOverflow=HorizontalWrapMode.Overflow;return t;}
    static Image Panel(Transform root,Vector2 anchor,Vector2 pos,Vector2 size,Color color){var o=new GameObject("Panel");o.transform.SetParent(root,false);var r=o.AddComponent<RectTransform>();r.anchorMin=r.anchorMax=anchor;r.pivot=anchor;r.anchoredPosition=pos;r.sizeDelta=size;var i=o.AddComponent<Image>();i.color=color;return i;}
    static class Sprites{static Sprite square,circle;public static Sprite Square=>square??(square=Make(false));public static Sprite Circle=>circle??(circle=Make(true));static Sprite Make(bool round){int n=round?64:2;var t=new Texture2D(n,n,TextureFormat.RGBA32,false);t.filterMode=FilterMode.Bilinear;var pixels=new Color[n*n];for(int y=0;y<n;y++)for(int x=0;x<n;x++){float dx=(x+.5f)/n*2-1,dy=(y+.5f)/n*2-1;pixels[y*n+x]=!round||dx*dx+dy*dy<=1?Color.white:Color.clear;}t.SetPixels(pixels);t.Apply();return Sprite.Create(t,new Rect(0,0,n,n),new Vector2(.5f,.5f),n);}}
}
