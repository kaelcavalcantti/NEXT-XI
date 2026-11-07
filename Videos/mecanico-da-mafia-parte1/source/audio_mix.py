import numpy as np, json, subprocess
from scipy.signal import butter, sosfilt, fftconvolve
SR=44100
T=json.load(open('timeline.json')); TOT=T['total']+0.3
N=int(TOT*SR); rng=np.random.default_rng(3)
sc={s['id']:s['start'] for s in T['scenes']}
def tt(d): return np.arange(int(d*SR))/SR
def lp(x,f,o=2): return sosfilt(butter(o,f,'low',fs=SR,output='sos'),x)
def hp(x,f,o=2): return sosfilt(butter(o,f,'high',fs=SR,output='sos'),x)
def bp(x,a,b,o=2): return sosfilt(butter(o,[a,b],'band',fs=SR,output='sos'),x)
def env(n,a=.005,r=.2):
    t=np.arange(n)/SR; e=np.minimum(1,t/max(a,1e-4))*np.exp(-t/max(r,1e-4)); return e
def place(buf,x,t,g=1.0,pan=0.0):
    i=int(t*SR); 
    if i>=len(buf): return
    x=x[:len(buf)-i]; l=np.cos((pan+1)*np.pi/4); r=np.sin((pan+1)*np.pi/4)
    buf[i:i+len(x),0]+=x*g*l*1.41; buf[i:i+len(x),1]+=x*g*r*1.41
def noise(d): return rng.standard_normal(int(d*SR))
def reverb(x,dec=1.6,mix=.25):
    n=int(dec*SR); ir=rng.standard_normal(n)*np.exp(-np.arange(n)/SR*5/dec); ir=lp(ir,5000); ir/=np.sqrt((ir**2).sum())
    w=fftconvolve(x,ir)[:len(x)]; return x*(1-mix)+w*mix
def saw(f,d):
    t=tt(d); return 2*((t*f)%1)-1
# ---------------- MUSIC ----------------
mus=np.zeros((N,2))
bpm=96; beat=60/bpm
def ks(f,d,bright=.5):  # karplus-strong pluck
    n=int(d*SR); p=int(SR/f); buf=rng.uniform(-1,1,p); out=np.zeros(n)
    for i in range(n):
        out[i]=buf[i%p]; buf[i%p]=bright*buf[i%p]+(1-bright)*0.996*buf[(i+1)%p] if False else 0.5*(buf[i%p]+buf[(i+1)%p])*0.996
    return out
# precompute pluck notes (Dm harmonic minor)
notes={'D3':146.8,'F3':174.6,'A3':220,'C#4':277.2,'D4':293.7,'E4':329.6,'F4':349.2,'G4':392,'A4':440,'Bb3':233.1,'G3':196,'E3':164.8,'A2':110,'D2':73.4,'Bb2':116.5,'C#3':138.6}
pl={k:lp(ks(v,1.2),4000) for k,v in notes.items()}
# section intensity by scene
inten={'s1':.8,'s2':.45,'s3':.9,'s4':.55,'s5':.75,'s6':1.0}
bars=[('Dm',['D3','A3','D4','F4','A3','D4']),('Dm',['D3','A3','D4','F4','A3','D4']),('Bb',['Bb2','F3','Bb3','D4','F3','Bb3']),('A',['A2','E3','A3','C#4','E3','A3'])]
end=sc['end']
t=0.0; bi=0
while t<end-0.2:
    sid=[s['id'] for s in T['scenes'] if s['start']<=t<s['end']][0]
    I=inten.get(sid,.5)
    chord,arp=bars[bi%4]
    for k,n in enumerate(arp):
        tn=t+k*beat/1.5
        if tn<end: place(mus,pl[n]*(0.5+0.5*I),tn,0.22,pan=(-.4 if k%2 else .4))
    # bass pulse
    root={'Dm':'D2','Bb':'Bb2','A':'A2'}[chord]; f0=notes[root]
    for k in range(4):
        tb=t+k*beat
        if tb<end:
            d=beat*.9; s=np.sin(2*np.pi*f0*tt(d))+.3*np.sin(4*np.pi*f0*tt(d)); place(mus,s*env(len(s),.01,.35),tb,.28*I)
            # kick / heartbeat
            if sid in('s3','s5','s6','s1') and k in (0,2):
                kd=.35; kt=tt(kd); kf=50+90*np.exp(-kt*25); kk=np.sin(2*np.pi*np.cumsum(kf)/SR)*np.exp(-kt*9); place(mus,kk,tb,.55*I)
            # tick
            if sid!='s2':
                for h in range(2):
                    th=tb+h*beat/2; c=hp(noise(.04),6000)*env(int(.04*SR),.001,.012); place(mus,c,th,.12*I,pan=.3)
    # string pad
    padf={'Dm':[146.8,174.6,220],'Bb':[116.5,146.8,174.6],'A':[110,138.6,164.8]}[chord]
    d=4*beat+.3; pad=sum(lp(saw(f*(1+.003*j),d),1200) for f in padf for j in (-1,1))
    pad*=np.minimum(1,tt(d)/.4)*np.minimum(1,(d-tt(d))/.4); place(mus,pad,t,.035*(0.6+I),pan=0)
    t+=4*beat; bi+=1
# s6 riser + drone
s6=sc['s6']; d=end-s6
dr=lp(saw(36.7,d)+saw(36.9,d),300)*np.linspace(.2,1,int(d*SR)); place(mus,dr,s6,.25)
r0=sc['s6']+6.0; d=end-r0; rz=bp(noise(d),300,6000)*np.linspace(0,1,int(d*SR))**2; place(mus,rz,r0,.25)
# vertigo tone
tv=tt(d); fv=220*(1+tv/d*0.5); place(mus,np.sin(2*np.pi*np.cumsum(fv)/SR)*np.linspace(0,1,len(tv))**2,r0,.08)
# end sting
d=2.4; st=np.zeros(int(d*SR))
for f in (36.7,73.4,110,146.8,174.6): st+=lp(saw(f,d),900)*np.exp(-tt(d)*1.4)
st+=lp(noise(d),200)*np.exp(-tt(d)*3)*2
place(mus,reverb(st,2.0,.4),end,.35)
# ---------------- SFX ----------------
sfx=np.zeros((N,2))
def whoosh(d=.35,f0=400,f1=3000,g=.35,t0=0,pan=0):
    n=noise(d); out=np.zeros_like(n); seg=int(.01*SR)
    for i in range(0,len(n),seg):
        u=i/len(n); f=f0+(f1-f0)*np.sin(u*np.pi); out[i:i+seg]=bp(n[i:i+seg+0],max(80,f*.6),min(18000,f*1.6),1)[:len(out[i:i+seg])]
    out*=np.sin(np.linspace(0,np.pi,len(out)))**2; place(sfx,out,t0,g,pan)
def clank(t0,f=1800,g=.3,pan=0):
    d=.35; x=sum(np.sin(2*np.pi*f*r*tt(d))*np.exp(-tt(d)*(14+r*6)) for r in (1,1.47,2.09,2.76)); place(sfx,x,t0,g,pan)
def thud(t0,g=.5,f=90,d=.25):
    k=np.sin(2*np.pi*np.cumsum(f+60*np.exp(-tt(d)*30))/SR)*np.exp(-tt(d)*18)+lp(noise(d),800)*np.exp(-tt(d)*30)*.5; place(sfx,k,t0,g)
def step(t0,g=.18,pan=0):
    d=.12; x=lp(noise(d),1800)*np.exp(-tt(d)*45)+np.sin(2*np.pi*120*tt(d))*np.exp(-tt(d)*40)*.5; place(sfx,x,t0,g,pan)
def horn(t0,d=.5,g=.15,f=(330,415),pan=0):
    x=sum(lp(saw(ff,d),2500) for ff in f)*np.minimum(1,tt(d)/.02)*np.minimum(1,(d-tt(d))/.05); place(sfx,x,t0,g,pan)
def pop(t0,g=.25,f=600):
    d=.09; x=np.sin(2*np.pi*np.cumsum(f*(1+2*np.exp(-tt(d)*60)))/SR)*np.exp(-tt(d)*40); place(sfx,x,t0,g)
def ding(t0,g=.25,f=1760):
    d=1.2; x=(np.sin(2*np.pi*f*tt(d))+.4*np.sin(2*np.pi*f*2.7*tt(d)))*np.exp(-tt(d)*4); place(sfx,x,t0,g)
# city ambience s1
d=sc['s2']; amb=lp(noise(d),600)*.6+bp(noise(d),800,3000)*.08
amb*=np.minimum(1,tt(d)/.5)*np.minimum(1,(d-tt(d))/.3); place(sfx,amb,0,.22)
horn(2.4,.35,.12,pan=-.5); horn(2.85,.5,.12,pan=-.5); horn(8.8,.4,.08,(370,466),pan=.6)
# SUV engine & pass-bys
d=sc['s2']; eng=lp(saw(42,d)+.5*saw(84.5,d),400)*(.5+.5*np.sin(2*np.pi*7*tt(d)))
envE=np.interp(tt(d),[0,4,5.2,6.4,9.6,11.5,11.8,12],[.1,.5,1,.6,.7,.5,1,.2]); place(sfx,eng*envE,0,.18)
whoosh(1.2,200,1800,.5,4.6,.6); whoosh(.9,200,2200,.6,11.3,.2)
# cut whooshes
cuts=[6.4,9.6]+[sc['s2']+x for x in (0,3.1,4.1,7.5)]+[sc['s3']+x for x in (0,2.9,5.0,8.2,11.0)]+[sc['s4']+x for x in (0,1.7,3.5,5.3)]+[sc['s5']+x for x in (0,1.9,3.3)]+[sc['s6']+x for x in (0,.9,6.0)]
for c in cuts: whoosh(.3,600,4000,.22,c-.15)
# s2 workshop: ratchet, coin, pop
s2=sc['s2']; amb2=lp(noise(10),300)*.3; place(sfx,amb2,s2,.15)
for i in range(int(3.1*9/(2*np.pi)*2)):
    clank(s2+.2+i*(2*np.pi/9)/2,2400 if i%2 else 1900,.06,.2)
tc=s2+3.1
for i in range(14):
    k=i/14; clank(tc+(1-(1-k)**1.6)*.95,3200,.05*(1-k)+.02,0)
ding(tc+.95,.08,2600)
pop(s2+7.5+.55,.45,500); whoosh(.5,1500,300,.25,s2+7.5+.5)   # eyes pop
# gulp
d=.25; g=np.sin(2*np.pi*np.cumsum(np.linspace(300,120,int(d*SR)))/SR)*np.exp(-tt(d)*10); place(sfx,g,s2+8.6,.3)
# s3 the visit
s3=sc['s3']; amb3=lp(noise(13.2),500)*.4; place(sfx,amb3,s3,.15)
d=2.6; e3=lp(saw(38,d)+.5*saw(76,d),350)*np.linspace(1,.4,int(d*SR)); place(sfx,e3,s3,.25)
d=.6; scr=bp(noise(d),1800,4500)*(np.sin(2*np.pi*12*tt(d))*.3+.7)*np.sin(np.linspace(0,np.pi,int(d*SR))); place(sfx,scr,s3+2.0,.12)
d=10; idle=lp(saw(32,d),200)*.5; place(sfx,idle*np.minimum(1,(d-tt(d))/1),s3+2.6,.12)
thud(s3+3.0,.35,140); clank(s3+3.0,700,.08); thud(s3+3.1,.3,130)
thud(s3+3.85,.6,70,.35)   # shoe hits street
for k in range(12): step(s3+4.1+k*.42,.16,-.2)
for k in range(10): step(s3+3.7+k*.33,.13,.3)
ding(s3+7.45,.12,3000)
thud(s3+8.2,.9,45,1.2)   # dramatic hit
# ka-ching on "dez vezes"
kc=s3+11.07
for i,f in enumerate((1568,2093,2637)): ding(kc+.05+i*.05,.12,f)
d=.15; place(sfx,hp(noise(d),3000)*np.exp(-tt(d)*25),kc,.25)
# s4 cash thuds, handshake, ding
s4=sc['s4']
for i in range(8): thud(s4+.15+i*.14+.25,.35,110+i*5)
d=.12; place(sfx,bp(noise(d),800,5000)*np.exp(-tt(d)*40),s4+3.5+.25,.5)  # slap
ding(s4+5.5,.18,1760)
# s5 garage
s5=sc['s5']; d=sc['s6']-s5; amb5=reverb(lp(noise(d),400)*.3,1.5,.4); place(sfx,amb5,s5,.15)
for k in range(5): step(s5+.1+k*.42,.2,0)
whoosh(.6,800,2500,.15,s5+.7)
for i in range(10): clank(s5+1.45+i*.025,4000+rng.uniform(-800,800),.06,.1)
b5=s5+1.9
for i in range(18): pop(b5+i*.04,.12,700+i*30)
for i in range(9): pop(b5+.55+i*.06,.14,900+i*80)
# jaw drop slide whistle
d=.7; f=np.linspace(1400,500,int(d*SR)); place(sfx,np.sin(2*np.pi*np.cumsum(f)/SR)*np.sin(np.linspace(0,np.pi,len(f))),b5+.3,.12)
hs=s5+3.3+1.1; thud(hs,.8,80,.4); clank(hs,500,.15)
for k in range(6): step(s5+3.3+1.8+k*.38,.2,.4)
d=.4; place(sfx,np.sin(2*np.pi*120*tt(d))*(rng.random(int(d*SR))>.5)*.5,s5+3.3+3.0,.06)
# s6 phone
s6=sc['s6']
d=.5; place(sfx,bp(noise(d),500,3000)*np.exp(-tt(d)*6),s6,.15)
for k in range(3):
    d=.18; bz=np.sign(np.sin(2*np.pi*150*tt(d)))*.5; place(sfx,lp(bz,1200),s6+.45+k*.25,.12)
for k in range(10):  # heartbeat
    tb=s6+2.2+k*.75
    if tb<sc['end']-.3: thud(tb,.35+k*.03,55,.3); thud(tb+.22,.25+k*.02,50,.3)
# ---------------- VOICE + MIX ----------------
vo=np.zeros(N)
for l in T['lines']:
    raw=subprocess.run(['ffmpeg','-v','error','-i',f"audio/{l['k']}.wav",'-f','f32le','-ac','1','-ar',str(SR),'-'],capture_output=True).stdout
    a=np.frombuffer(raw,np.float32).astype(float)
    if l['speaker']=='h': a=reverb(a,.6,.15)
    i=int(l['start']*SR); vo[i:i+len(a)]+=a[:N-i]
venv=np.abs(vo); venv=lp(venv,4); venv=np.clip(venv/ (np.percentile(venv[venv>1e-4],90)+1e-9),0,1)
duck=1-.55*np.clip(venv*2,0,1)
mus*=duck[:,None]; mus=reverb(mus.T.reshape(-1),.01,0).reshape(2,-1).T if False else mus
mix=mus*0.5+sfx*0.75+np.stack([vo,vo],1)*1.0
print("rms vo",np.sqrt((vo**2).mean()),"mus",np.sqrt((mus**2).mean()),"sfx",np.sqrt((sfx**2).mean()),"peaks",np.abs(vo).max(),np.abs(mus).max(),np.abs(sfx).max())
mix/=max(1e-9,np.abs(mix).max())/0.95
import wave
pcm=(np.clip(mix,-1,1)*32767).astype(np.int16)
with wave.open('audio/mix.wav','wb') as w: w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
print('done',TOT)
