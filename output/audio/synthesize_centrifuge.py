import math, random, wave, struct
from pathlib import Path
out=Path(r'E:\code\FluxForge\output\audio')
rate=44100
for name,duration,base,rough in [('centrifuge_soft_160ms',.16,260,.12),('centrifuge_click_120ms',.12,340,.22),('centrifuge_spin_190ms',.19,200,.09)]:
 rng=random.Random(42); vals=[]; phase=0; low=0
 for i in range(round(rate*duration)):
  t=i/rate; x=t/duration
  freq=base*(1.55-.65*x)
  phase+=2*math.pi*freq/rate
  low=.78*low+.22*rng.uniform(-1,1)
  env=min(1,t/.006)*(1-x)**1.7
  motor=(math.sin(phase)*.43+math.sin(phase*2)*.15+math.sin(phase*3)*.07)*( .78+.22*math.sin(2*math.pi*65*t))
  click=low*rough*math.exp(-t/ .018)
  vals.append((motor*env+click)*.65)
 with wave.open(str(out/(name+'.wav')),'wb') as f:
  f.setnchannels(1); f.setsampwidth(2); f.setframerate(rate)
  f.writeframes(b''.join(struct.pack('<h',max(-32768,min(32767,round(v*32767)))) for v in vals))
 print(name,duration,'peak',round(max(abs(v) for v in vals),3))
