import React,{useState} from 'react';
import Heart from 'lucide-react/icons/heart';
import Zap from 'lucide-react/icons/zap';
import InfinityIcon from 'lucide-react/icons/infinity';
import Link from 'lucide-react/icons/link';
import Download from 'lucide-react/icons/download';
import Unplug from 'lucide-react/icons/unplug';
import Command from 'lucide-react/icons/command';
import ChevronRight from 'lucide-react/icons/chevron-right';
import TrainerControls from './TrainerControls.jsx';
import ConnectionCard from './ConnectionCard.jsx';
const icons=[Heart,Zap,InfinityIcon],ID=2738490;
export default function SolTrainer({status,bindings,parameters,action,api,busy,navigate,notify}){
 const [gold,setGold]=useState('100');
 return <div className="trainer-layout"><section className="trainer-panel"><div className="section-title"><h2>Desafie a sorte.</h2><span>{status.options.filter(Boolean).length} ativas</span></div><p className="section-copy">Conecte o trainer e entre em uma partida para usar as opções.</p>
 <TrainerControls gameId={ID} status={status} bindings={bindings} parameters={parameters} action={action} api={api} busy={busy} notify={notify}/>
 {status.values&&<p className="section-copy sol-values">Vida: {status.values.health}/{status.values.maxHealth} · Ouro: {status.values.gold} · Poder: {status.values.charge}/{status.values.maxCharge}</p>}
 </section><ConnectionCard id={ID} status={status} api={api} action={action} busy={busy} navigate={navigate}/></div>;
}
