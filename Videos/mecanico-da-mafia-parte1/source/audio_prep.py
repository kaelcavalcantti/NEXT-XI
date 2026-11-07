import json,subprocess,re
def dur(f): return float(subprocess.run(['ffprobe','-v','error','-show_entries','format=duration','-of','csv=p=0',f],capture_output=True,text=True).stdout)
F={
 'n':"atempo=1.15,highpass=f=70,equalizer=f=180:t=q:w=1:g=3,equalizer=f=3000:t=q:w=1.5:g=4,acompressor=threshold=-18dB:ratio=3:attack=5:release=80,volume=1.5",
 'h':"asetrate=44100*0.86,aresample=44100,atempo=1.2,highpass=f=60,equalizer=f=150:t=q:w=1:g=5,acompressor=threshold=-18dB:ratio=3,aecho=0.8:0.5:40:0.18,volume=1.6",
 'p':"asetrate=44100*0.9,aresample=44100,atempo=1.15,highpass=f=350,lowpass=f=3200,acrusher=bits=10:mix=0.3,equalizer=f=1500:t=q:w=1:g=6,acompressor=threshold=-20dB:ratio=4,volume=2.2",
}
L=json.load(open('lines.json'))
D={}
for k,t in L:
    subprocess.run(['ffmpeg','-y','-v','error','-i',f'audio/{k}_trim.wav','-af',F[k[0]],'-ar','44100','-ac','1',f'audio/{k}.wav'])
    D[k]=dur(f'audio/{k}.wav')
cap={ # caption text (correct spelling)
 'n1':"E se você fosse um mecânico de Guadalajara, no México, e uma das organizações criminosas mais perigosas do país estivesse procurando exatamente alguém como você? Seu nome seria Alejandro, e sua vida estava prestes a mudar para sempre.",
 'n2':"Alejandro era um mecânico extremamente talentoso, mas ganhava pouco. Sua mãe estava enfrentando uma doença grave nos rins, e o tratamento custava mais dinheiro do que ele poderia imaginar.",
 'n3':"Até que, numa tarde, dois homens ligados à máfia mexicana apareceram em sua oficina. Eles já conheciam sua reputação e tinham uma proposta.",
 'h3':"Precisamos de um mecânico como você. Pagamos dez vezes mais do que você ganha aqui.",
 'n4':"Alejandro ficou desconfiado. Mas, pensando no tratamento da mãe, decidiu aceitar. Afinal, ele acreditava que estaria apenas consertando carros.",
 'n5':"No primeiro dia, ele recebeu uma quantia absurda de dinheiro. Mas, quando terminou de consertar o primeiro veículo, recebeu uma ordem inesperada.",
 'p6':"Escute bem, Alejandro. A partir de hoje, você trabalha exclusivamente para nós.",
 'n6':"E naquele momento ele percebeu que talvez tivesse cometido o maior erro da sua vida.",
}
# schedule
plan=[('s1',['n1']),('s2',['n2']),('s3',['n3','h3']),('s4',['n4']),('s5',['n5']),('s6',['p6','n6'])]
t=0.35; scenes=[]; lines=[]
for s,ks in plan:
    st=max(0,t-0.35) if s!='s1' else 0
    for i,k in enumerate(ks):
        if i>0: t+=0.3
        lines.append({'k':k,'start':round(t,3),'dur':round(D[k],3),'speaker':k[0]})
        t+=D[k]
    t+=0.3
    scenes.append({'id':s,'start':round(st,3)})
for i,s in enumerate(scenes):
    s['end']=scenes[i+1]['start'] if i+1<len(scenes) else None
# s6: end card after
endcard=t+0.2
scenes[-1]['end']=round(endcard,3)
scenes.append({'id':'end','start':round(endcard,3),'end':round(endcard+2.2,3)})
total=round(endcard+2.2,3)
def syl(w):
    w=w.lower(); n=len(re.findall(r'[aeiouáéíóúâêôãõày]+',w)); return max(1,n)
for ln in lines:
    words=cap[ln['k']].split()
    wts=[]
    for w in words:
        wt=syl(w)+0.4
        if re.search(r'[,;]$',w): wt+=1.6
        if re.search(r'[.?!]$',w): wt+=2.2
        wts.append(wt)
    S=sum(wts); acc=ln['start']; ws=[]
    for w,wt in zip(words,wts):
        d=wt/S*ln['dur']; ws.append({'w':w,'t':round(acc,3)}); acc+=d
    ln['words']=ws
json.dump({'total':total,'scenes':scenes,'lines':lines},open('timeline.json','w'),indent=1)
print(total); [print(s) for s in scenes]; [print(l['k'],l['start'],l['dur']) for l in lines]
