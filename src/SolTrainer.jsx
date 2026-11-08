import {t} from './i18n.js';
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
 return (
  <div className="trainer-layout"><section className="trainer-panel"><div className="section-title"><h2>{t('Try your luck.')}</h2><span>{t(status.options.filter(Boolean).length)}{t(' active')}</span></div><p className="section-copy">{t('Connect the trainer and enter a game to use these options.')}</p>
   <TrainerControls gameId={ID} status={status} bindings={bindings} parameters={parameters} action={action} api={api} busy={busy} notify={notify}/>
   {status.values&&<p className="section-copy sol-values">{t('Health: ')}{t(status.values.health)}{t('/')}{t(status.values.maxHealth)}{t(' · Gold: ')}{t(status.values.gold)}{t(' · Power: ')}{t(status.values.charge)}{t('/')}{t(status.values.maxCharge)}</p>}
   </section><ConnectionCard id={ID} status={status} api={api} action={action} busy={busy} navigate={navigate}/></div>
 );
}
