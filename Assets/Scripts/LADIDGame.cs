using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class LADIDGame : MonoBehaviour
{
    const float W = 8f, H = 5f;
    enum World { Volcano, Tidal, Earthquake, AcidRain }
    sealed class FissureData
    {
        public bool horizontal, triggered;
        public float coordinate;
        public int side;
    }
    Transform player;
    Transform hazardsRoot;
    Transform splitVisualRoot;
    SpriteRenderer leftFieldRenderer, rightFieldRenderer;
    SpriteRenderer playerRenderer;
    Color playerColor = new Color(.88f,.96f,1f);
    Camera cam;
    Text worldText, timerText, livesText, messageText, stageText;
    Image banner, flash, shatter;
    World world;
    World secondaryWorld;
    readonly List<FissureData> fissures = new List<FissureData>();
    float phaseTime, phaseDuration, survived, flashTime, damageTintTime, health = 100f;
    int stage = 1;
    bool ended, changing, falling, overlapMode;

    void Start()
    {
        Application.targetFrameRate = 60;
        hazardsRoot = new GameObject("Dimension Hazards").transform;
        cam = new GameObject("Camera").AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 6.5f; cam.transform.position = new Vector3(0,0,-10);
        cam.backgroundColor = new Color(.12f,.09f,.1f);
        leftFieldRenderer=Rect("Field Left",new Vector3(-4,0),8,10,new Color(.2f,.19f,.19f),0).GetComponent<SpriteRenderer>();
        rightFieldRenderer=Rect("Field Right",new Vector3(4,0),8,10,new Color(.2f,.19f,.19f),0).GetComponent<SpriteRenderer>();
        for (int i=-7;i<=7;i++) Rect("Grid",new Vector3(i,0,.1f),.018f,10,new Color(1,1,1,.04f),1);
        for (int i=-4;i<=4;i++) Rect("Grid",new Vector3(0,i,.1f),16,.018f,new Color(1,1,1,.04f),1);
        Rect("Top edge",new Vector3(0,5.1f),16.3f,.2f,new Color(.9f,.3f,.16f),1);
        Rect("Bottom edge",new Vector3(0,-5.1f),16.3f,.2f,new Color(.9f,.3f,.16f),1);
        Rect("Left edge",new Vector3(-8.1f,0),.2f,10.3f,new Color(.9f,.3f,.16f),1);
        Rect("Right edge",new Vector3(8.1f,0),.2f,10.3f,new Color(.9f,.3f,.16f),1);
        BuildSplitVisual();
        player = Rect("Player",Vector3.zero,.52f,.52f,playerColor,5).transform;
        playerRenderer = player.GetComponent<SpriteRenderer>();
        BuildHud();
        world=(World)Random.Range(0,4);secondaryWorld=world;SetWorld(world,true);
        StartCoroutine(PhaseLoop()); StartCoroutine(VolcanoLoop()); StartCoroutine(TideLoop()); StartCoroutine(EarthquakeLoop()); StartCoroutine(AcidRainLoop());
    }

    void Update()
    {
        if (ended) { if (Input.GetKeyDown(KeyCode.R)) Restart(); return; }
        float dt=Time.deltaTime; survived+=dt; phaseTime-=dt;
        Vector2 v=new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));
        if(v.sqrMagnitude>1) v.Normalize();
        Vector3 p=player.position+(Vector3)(v*7f*dt); p.x=Mathf.Clamp(p.x,-7.65f,7.65f); p.y=Mathf.Clamp(p.y,-4.65f,4.65f); player.position=p;
        timerText.text="NEXT COLLAPSE  "+Mathf.CeilToInt(phaseTime).ToString("00")+"s";
        stageText.text="STAGE  "+stage.ToString("00");
        ApplyCoreContactDamage(dt);
        ApplyFissureContact();
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
        stageText=Label(c.transform,"",16,TextAnchor.UpperLeft,new Vector2(0,1),new Vector2(0,1),new Vector2(28,-104),new Vector2(300,28),new Color(.75f,.85f,1f));
        messageText=Label(c.transform,"",16,TextAnchor.MiddleCenter,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,22),new Vector2(900,44),Color.white);
        banner=Panel(c.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(560,100),new Color(.4f,.1f,.07f,.94f));
        Label(banner.transform,"",26,TextAnchor.MiddleCenter,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Color.white);
        flash=Panel(c.transform,Vector2.zero,Vector2.zero,Vector2.zero,new Color(1,.1f,.1f,0));
        flash.rectTransform.anchorMax=Vector2.one; flash.rectTransform.offsetMin=Vector2.zero; flash.rectTransform.offsetMax=Vector2.zero;
        shatter=Panel(c.transform,Vector2.zero,Vector2.zero,Vector2.zero,Color.white);
        shatter.rectTransform.anchorMax=Vector2.one; shatter.rectTransform.offsetMin=Vector2.zero; shatter.rectTransform.offsetMax=Vector2.zero;
        shatter.sprite=CreateCrackSprite(); shatter.type=Image.Type.Simple; shatter.raycastTarget=false; shatter.color=new Color(1,1,1,0);
        banner.gameObject.SetActive(false); flash.transform.SetAsLastSibling(); shatter.transform.SetAsLastSibling(); shatter.gameObject.SetActive(false);
    }

    IEnumerator PhaseLoop()
    {
        while(!ended){yield return new WaitForSeconds(phaseDuration); if(ended)yield break; changing=true; ClearHazards(); StartCoroutine(PlayShatter(1.5f)); banner.gameObject.SetActive(true); banner.GetComponentInChildren<Text>().text="DIMENSION COLLAPSE  •  STAGE "+(stage+1).ToString("00"); yield return new WaitForSeconds(1.5f); stage++; World next=RandomWorld(); overlapMode=stage>=3; secondaryWorld=overlapMode?RandomWorldExcept(next):next; SetWorld(next,false); changing=false;}
    }
    World RandomWorld(){World next=world;while(next==world)next=(World)Random.Range(0,4);return next;}
    World RandomWorldExcept(World excluded){World next=excluded;while(next==excluded)next=(World)Random.Range(0,4);return next;}
    bool IsWorldActive(World candidate){return world==candidate||(overlapMode&&secondaryWorld==candidate);}
    int GetWorldSide(World candidate){if(!overlapMode)return 0;return candidate==world?-1:candidate==secondaryWorld?1:0;}
    Vector2 RandomWorldPosition(World dimension,float minX,float maxX,float minY,float maxY)
    {
        int side=GetWorldSide(dimension);
        if(side<0)maxX=Mathf.Min(maxX,-.55f);
        else if(side>0)minX=Mathf.Max(minX,.55f);
        return new Vector2(Random.Range(minX,maxX),Random.Range(minY,maxY));
    }
    string WorldName(World dimension)
    {
        if(dimension==World.Volcano)return "VOLCANO";
        if(dimension==World.Tidal)return "TIDAL";
        if(dimension==World.Earthquake)return "EARTHQUAKE";
        return "ACID RAIN";
    }
    Color WorldFieldColor(World dimension)
    {
        if(dimension==World.Volcano)return new Color(.25f,.13f,.11f);
        if(dimension==World.Tidal)return new Color(.07f,.25f,.32f);
        if(dimension==World.Earthquake)return new Color(.3f,.22f,.14f);
        return new Color(.13f,.28f,.1f);
    }
    void BuildSplitVisual()
    {
        splitVisualRoot=new GameObject("Mirror Dimension Divider").transform;
        GameObject seam=Rect("Mirror seam",new Vector3(0,0,-.15f),.075f,10f,new Color(.72f,.92f,1f,.95f),3);
        seam.transform.SetParent(splitVisualRoot,true);
        for(int i=0;i<7;i++)
        {
            float y=-4.2f+i*1.35f;float direction=i%2==0?1f:-1f;
            GameObject branch=Rect("Mirror fracture",new Vector3(direction*.22f,y,-.15f),.58f,.025f,new Color(.75f,.94f,1f,.85f),3);
            branch.transform.SetParent(splitVisualRoot,true);branch.transform.rotation=Quaternion.Euler(0,0,direction*32f);
            GameObject twig=Rect("Mirror fracture",new Vector3(direction*.42f,y+direction*.16f,-.15f),.31f,.018f,new Color(.65f,.86f,1f,.75f),3);
            twig.transform.SetParent(splitVisualRoot,true);twig.transform.rotation=Quaternion.Euler(0,0,-direction*48f);
        }
        splitVisualRoot.gameObject.SetActive(false);
    }
    IEnumerator PlayShatter(float duration)
    {
        shatter.gameObject.SetActive(true); float elapsed=0f;
        while(elapsed<duration)
        {
            elapsed+=Time.deltaTime; float progress=Mathf.Clamp01(elapsed/duration);
            float alpha=progress<.12f?Mathf.Lerp(0f,.9f,progress/.12f):Mathf.Lerp(.9f,0f,(progress-.12f)/.88f);
            shatter.color=new Color(1,1,1,alpha); yield return null;
        }
        shatter.color=new Color(1,1,1,0); shatter.gameObject.SetActive(false);
    }
    void SetWorld(World next,bool first)
    {
        if(!first) ClearHazards();
        world=next; phaseDuration=Random.Range(10f,20f); phaseTime=phaseDuration;
        splitVisualRoot.gameObject.SetActive(overlapMode);
        if(overlapMode)
        {
            leftFieldRenderer.color=WorldFieldColor(world);rightFieldRenderer.color=WorldFieldColor(secondaryWorld);cam.backgroundColor=new Color(.035f,.035f,.05f);
            worldText.text="OVERLAP  /  "+WorldName(world)+"  +  "+WorldName(secondaryWorld);
            messageText.text="DIMENSION OVERLAP  •  LEFT: "+WorldName(world)+"  /  RIGHT: "+WorldName(secondaryWorld);
        }
        else
        {
            leftFieldRenderer.color=rightFieldRenderer.color=new Color(.2f,.19f,.19f);
            cam.backgroundColor=next==World.Volcano?new Color(.12f,.09f,.1f):next==World.Tidal?new Color(.04f,.19f,.27f):next==World.Earthquake?new Color(.16f,.12f,.09f):new Color(.08f,.16f,.09f);
            worldText.text=next==World.Volcano?"01  /  VOLCANIC WASTELAND":next==World.Tidal?"02  /  TIDAL WORLD":next==World.Earthquake?"03  /  FRACTURED EARTH":"04  /  ACID RAIN";
            messageText.text=next==World.Volcano?"VOLCANIC WORLD  •  MOVE OUT OF THE RED WARNING CIRCLES":next==World.Tidal?"TIDAL WORLD  •  EVADE THE HORIZONTAL WAVE BY MOVING UP OR DOWN":next==World.Earthquake?"EARTHQUAKE WORLD  •  LEAVE THE CRACKING GROUND BEFORE IT OPENS":"ACID RAIN WORLD  •  DODGE THE FALLING DROPS";
        }
        if(!first){player.position=Vector3.zero; banner.GetComponentInChildren<Text>().text=overlapMode?"ENTERING: DIMENSION OVERLAP":next==World.Volcano?"ENTERING: VOLCANIC WORLD":next==World.Tidal?"ENTERING: TIDAL WORLD":next==World.Earthquake?"ENTERING: FRACTURED EARTH":"ENTERING: ACID RAIN"; banner.color=overlapMode?new Color(.2f,.24f,.48f,.94f):next==World.Volcano?new Color(.45f,.13f,.06f,.94f):next==World.Tidal?new Color(.02f,.25f,.4f,.94f):next==World.Earthquake?new Color(.32f,.2f,.1f,.94f):new Color(.12f,.38f,.12f,.94f); StartCoroutine(HideBanner());}
        else banner.gameObject.SetActive(false);
    }
    IEnumerator HideBanner(){yield return new WaitForSeconds(1.1f);if(!changing)banner.gameObject.SetActive(false);}
    IEnumerator VolcanoLoop(){while(true){yield return new WaitForSeconds(Random.Range(.65f,1.2f));if(ended||changing||!IsWorldActive(World.Volcano))continue;int projectileCount=Mathf.Min(stage,6);for(int i=0;i<projectileCount;i++)StartCoroutine(Meteor(RandomWorldPosition(World.Volcano,-7.3f,7.3f,-4.3f,4.3f)));}}
    IEnumerator Meteor(Vector2 at)
    {
        float impactSize=Random.Range(1.2f,3.6f);
        GameObject warn=Disc("Impact warning",at,impactSize,new Color(1,.12f,.04f,.4f),2);
        float t=0;while(t<1.05f){if(!warn||ended||changing||!IsWorldActive(World.Volcano)){if(warn)Destroy(warn);yield break;}t+=Time.deltaTime;yield return null;}
        if(!warn||ended||changing||!IsWorldActive(World.Volcano))yield break;
        Destroy(warn);GameObject hit=Disc("Meteor impact",at,impactSize,new Color(1,.28f,.06f),3);GameObject core=Disc("Impact core",at,impactSize*.5f,new Color(1,.78f,.25f),4);StartCoroutine(ShrinkCore(core,10f));
        if(Vector2.Distance(player.position,at)<impactSize*.5f+.25f)TakeDamage(30f);yield return new WaitForSeconds(.28f);Destroy(hit);
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
    IEnumerator TideLoop(){while(true){float intervalReduction=(stage-1)*.25f;yield return new WaitForSeconds(Mathf.Max(.65f,Random.Range(3f,4.2f)-intervalReduction));if(ended||changing||!IsWorldActive(World.Tidal))continue;StartCoroutine(Wave(Random.Range(-2.6f,2.6f),GetWorldSide(World.Tidal)));}}
    IEnumerator EarthquakeLoop(){while(true){yield return new WaitForSeconds(Random.Range(2.7f,3.8f));if(ended||changing||!IsWorldActive(World.Earthquake))continue;int fissuresPerQuake=Mathf.Min(1+(stage-1)/2,5);for(int i=0;i<fissuresPerQuake;i++)StartCoroutine(Fissure(GetWorldSide(World.Earthquake)));}}
    IEnumerator AcidRainLoop(){while(true){yield return new WaitForSeconds(Random.Range(.06f,.11f));if(ended||changing||!IsWorldActive(World.AcidRain))continue;StartCoroutine(AcidDrop());}}
    IEnumerator AcidDrop()
    {
        float x=RandomWorldPosition(World.AcidRain,-7.8f,7.8f,0f,0f).x;float size=Random.Range(.11f,.2f);float speed=Random.Range(8f,12f)+(stage-1)*1.25f;
        GameObject drop=Rect("Acid droplet",new Vector3(x,Random.Range(5.4f,6.4f),-.25f),size*.65f,size,new Color(.55f,1f,.18f,.92f),3);
        drop.transform.SetParent(hazardsRoot,true);
        while(drop&&!ended&&!changing&&IsWorldActive(World.AcidRain)&&drop.transform.position.y> -5.6f)
        {
            Vector3 position=drop.transform.position;position.y-=speed*Time.deltaTime;drop.transform.position=position;
            if(Mathf.Abs(player.position.x-position.x)<.34f&&Mathf.Abs(player.position.y-position.y)<.38f){TakeDamage(5f);Destroy(drop);yield break;}
            yield return null;
        }
        if(drop)Destroy(drop);
    }
    IEnumerator Fissure(int side)
    {
        bool horizontal=Random.value>.5f;
        float centerX=side==0?0f:side*4f;float width=side==0?16f:8f;
        float coordinate=horizontal?Random.Range(-3.8f,3.8f):side<0?Random.Range(-7.3f,-.6f):side>0?Random.Range(.6f,7.3f):Random.Range(-6.5f,6.5f);
        Vector3 position=horizontal?new Vector3(centerX,coordinate,-.2f):new Vector3(coordinate,0,-.2f);
        GameObject crack=Rect("Fissure warning",position,horizontal?width:.24f,horizontal?.24f:10f,new Color(1,.35f,.08f,.78f),2);
        crack.transform.SetParent(hazardsRoot,true);
        yield return new WaitForSeconds(1.1f);
        if(!crack||ended||changing||!IsWorldActive(World.Earthquake)){if(crack)Destroy(crack);yield break;}
        crack.name="Ground fissure";crack.GetComponent<SpriteRenderer>().color=new Color(.035f,.025f,.02f,1f);
        FissureData data=new FissureData{horizontal=horizontal,coordinate=coordinate,side=side};fissures.Add(data);
        yield return new WaitForSeconds(2.4f);
        fissures.Remove(data);Destroy(crack);
    }
    void ApplyFissureContact()
    {
        if(!IsWorldActive(World.Earthquake)||falling)return;
        foreach(FissureData fissure in fissures)
        {
            if(fissure.triggered)continue;
            bool insideSide=fissure.side==0||(fissure.side<0?player.position.x<0f:player.position.x>=0f);
            bool overGap=insideSide&&(fissure.horizontal?Mathf.Abs(player.position.y-fissure.coordinate)<.47f:Mathf.Abs(player.position.x-fissure.coordinate)<.47f);
            if(!overGap)continue;
            fissure.triggered=true;falling=true;TakeDamage(20f);StartCoroutine(FallAndRecover(fissure));break;
        }
    }
    IEnumerator FallAndRecover(FissureData fissure)
    {
        float side=player.position.x+player.position.y<0f?1f:-1f;
        Vector3 safePosition=player.position;
        if(fissure.horizontal)safePosition.y=Mathf.Clamp(fissure.coordinate+side*.85f,-4.65f,4.65f);
        else safePosition.x=Mathf.Clamp(fissure.coordinate+side*.85f,-7.65f,7.65f);
        float elapsed=0f;
        while(elapsed<.22f)
        {
            elapsed+=Time.deltaTime;float scale=Mathf.Lerp(.52f,.06f,elapsed/.22f);player.localScale=Vector3.one*scale;yield return null;
        }
        player.position=safePosition;player.localScale=Vector3.one*.52f;falling=false;
    }
    IEnumerator Wave(float y,int side)
    {
        float band=Random.Range(1.05f,1.55f);bool left=Random.value>.5f;
        float minX=side<0?-8f:side>0?0f:-8f,maxX=side<0?0f:side>0?8f:8f;
        float x=left?minX-2f:maxX+2f,end=left?maxX+2f:minX-2f;
        var wave=Rect("Tsunami",new Vector3(x,y,-.3f),2,band*2,new Color(.12f,.75f,.95f,.72f),3);
        wave.transform.SetParent(hazardsRoot,true);
        banner.color=new Color(.02f,.26f,.4f,.92f);banner.GetComponentInChildren<Text>().text=left?"WAVE INCOMING  →":"←  WAVE INCOMING";banner.gameObject.SetActive(true);yield return new WaitForSeconds(1.05f);
        if(!wave||ended||changing||!IsWorldActive(World.Tidal)){if(wave)Destroy(wave);yield break;}banner.gameObject.SetActive(false);
        bool hit=false;while(wave&&!ended&&!changing&&IsWorldActive(World.Tidal)&&(left?wave.transform.position.x<end:wave.transform.position.x>end))
        {
            float dt=Time.deltaTime;var p=wave.transform.position;p.x+=(left?16:-16)*dt;wave.transform.position=p;
            bool touching=Mathf.Abs(player.position.y-y)<band+.34f&&Mathf.Abs(player.position.x-p.x)<1.25f;
            if(touching&&!hit){hit=true;TakeDamage(40f);player.position=new Vector3(player.position.x,Mathf.Clamp(y+(player.position.y<y?-1:1)*(band+.7f),-4.6f,4.6f),0);}
            yield return null;
        }if(wave)Destroy(wave);
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
    void ClearHazards(){for(int i=hazardsRoot.childCount-1;i>=0;i--)Destroy(hazardsRoot.GetChild(i).gameObject);fissures.Clear();}
    void Restart(){StopAllCoroutines();ClearHazards();health=100f;survived=0;stage=1;ended=false;changing=false;falling=false;overlapMode=false;flashTime=0;damageTintTime=0;playerRenderer.color=playerColor;player.localScale=Vector3.one*.52f;player.position=Vector3.zero;world=(World)Random.Range(0,4);secondaryWorld=world;SetWorld(world,true);StartCoroutine(PhaseLoop());StartCoroutine(VolcanoLoop());StartCoroutine(TideLoop());StartCoroutine(EarthquakeLoop());StartCoroutine(AcidRainLoop());}
    GameObject Rect(string n,Vector3 p,float w,float h,Color c,int order){var o=new GameObject(n);o.transform.position=p;o.transform.localScale=new Vector3(w,h,1);var r=o.AddComponent<SpriteRenderer>();r.sprite=Sprites.Square;r.color=c;r.sortingOrder=order;return o;}
    GameObject Disc(string n,Vector2 p,float s,Color c,int order){var o=new GameObject(n);o.transform.SetParent(hazardsRoot,false);o.transform.position=p;o.transform.localScale=Vector3.one*s;var r=o.AddComponent<SpriteRenderer>();r.sprite=Sprites.Circle;r.color=c;r.sortingOrder=order;return o;}
    static Sprite CreateCrackSprite()
    {
        const int width=512,height=288; var pixels=new Color[width*height]; var random=new System.Random(27);
        Vector2 center=new Vector2(width*.5f,height*.5f);
        for(int i=0;i<10;i++)DrawCrack(pixels,width,height,center,Mathf.PI*2*i/10f+(float)(random.NextDouble()-.5)*.16f,random.Next(190,280),random,0);
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false); texture.filterMode=FilterMode.Bilinear; texture.wrapMode=TextureWrapMode.Clamp;
        texture.SetPixels(pixels); texture.Apply(); return Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f),100f);
    }
    static void DrawCrack(Color[] pixels,int width,int height,Vector2 start,float angle,float length,System.Random random,int depth)
    {
        Vector2 point=start; int segments=Mathf.Max(4,Mathf.RoundToInt(length/24f));
        for(int i=0;i<segments;i++)
        {
            angle+=(float)(random.NextDouble()-.5)*.32f;
            float step=length/segments; Vector2 next=point+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*step;
            DrawCrackLine(pixels,width,height,point,next,depth==0?1:0);
            if(depth<2&&i==segments/2&&length>70f)
            {
                float branchAngle=angle+(random.Next(0,2)==0?-1f:1f)*(float)(.55+random.NextDouble()*.55);
                DrawCrack(pixels,width,height,point,branchAngle,length*(float)(.35+random.NextDouble()*.2),random,depth+1);
            }
            point=next;
            if(point.x<0||point.x>=width||point.y<0||point.y>=height)break;
        }
    }
    static void DrawCrackLine(Color[] pixels,int width,int height,Vector2 from,Vector2 to,int radius)
    {
        int x0=Mathf.RoundToInt(from.x),y0=Mathf.RoundToInt(from.y),x1=Mathf.RoundToInt(to.x),y1=Mathf.RoundToInt(to.y);
        int dx=Mathf.Abs(x1-x0),sx=x0<x1?1:-1,dy=-Mathf.Abs(y1-y0),sy=y0<y1?1:-1,error=dx+dy;
        while(true)
        {
            for(int oy=-radius;oy<=radius;oy++)for(int ox=-radius;ox<=radius;ox++)
            {
                int x=x0+ox,y=y0+oy;if(x>=0&&x<width&&y>=0&&y<height)pixels[y*width+x]=Color.white;
            }
            if(x0==x1&&y0==y1)break;int twice=2*error;
            if(twice>=dy){error+=dy;x0+=sx;}if(twice<=dx){error+=dx;y0+=sy;}
        }
    }
    static Text Label(Transform root,string value,int size,TextAnchor align,Vector2 amin,Vector2 amax,Vector2 pos,Vector2 dim,Color color){var o=new GameObject("Label");o.transform.SetParent(root,false);var r=o.AddComponent<RectTransform>();r.anchorMin=amin;r.anchorMax=amax;r.pivot=new Vector2(amin.x==amax.x?amin.x:.5f,amin.y==amax.y?amin.y:.5f);r.anchoredPosition=pos;r.sizeDelta=dim;var t=o.AddComponent<Text>();t.text=value;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.fontStyle=FontStyle.Bold;t.alignment=align;t.color=color;t.horizontalOverflow=HorizontalWrapMode.Overflow;return t;}
    static Image Panel(Transform root,Vector2 anchor,Vector2 pos,Vector2 size,Color color){var o=new GameObject("Panel");o.transform.SetParent(root,false);var r=o.AddComponent<RectTransform>();r.anchorMin=r.anchorMax=anchor;r.pivot=anchor;r.anchoredPosition=pos;r.sizeDelta=size;var i=o.AddComponent<Image>();i.color=color;return i;}
    static class Sprites{static Sprite square,circle;public static Sprite Square=>square??(square=Make(false));public static Sprite Circle=>circle??(circle=Make(true));static Sprite Make(bool round){int n=round?64:2;var t=new Texture2D(n,n,TextureFormat.RGBA32,false);t.filterMode=FilterMode.Bilinear;var pixels=new Color[n*n];for(int y=0;y<n;y++)for(int x=0;x<n;x++){float dx=(x+.5f)/n*2-1,dy=(y+.5f)/n*2-1;pixels[y*n+x]=!round||dx*dx+dy*dy<=1?Color.white:Color.clear;}t.SetPixels(pixels);t.Apply();return Sprite.Create(t,new Rect(0,0,n,n),new Vector2(.5f,.5f),n);}}
}
