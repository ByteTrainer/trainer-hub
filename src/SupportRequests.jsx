import {t} from './i18n.js';
import React, {useState} from 'react';
import MessageSquarePlus from 'lucide-react/icons/message-square-plus';
import ArrowUpRight from 'lucide-react/icons/arrow-up-right';
import confirmedGames from './confirmed-games.json';

const tabs = [{id:'discord',label:'Solicitar um jogo'},{id:'confirmed',label:'Jogos confirmados'}];
const covers = import.meta.glob('./assets/confirmed/*.jpg',{eager:true,query:'?url',import:'default'});

export default function SupportRequests({games,api,action,busy}){
 const [active,setActive]=useState('discord');
 const confirmed=confirmedGames.filter(game=>!games.some(supported=>supported.id===game.id));

 function moveTab(event,index){
  const {key}=event;
  if(!['ArrowLeft','ArrowRight','Home','End'].includes(key))return;
  event.preventDefault();
  const next=key==='Home'?0:key==='End'?tabs.length-1:(index+(key==='ArrowRight'?1:-1)+tabs.length)%tabs.length;
  setActive(tabs[next].id);
  event.currentTarget.parentElement.children[next].focus();
 }

 return (
  <>
   <div className="page-heading"><div><h1>{t('Pedidos de suporte.')}</h1><p>{t('Sugira um jogo ou veja quais trainers já estão confirmados.')}</p></div></div>
   <div className="catalog-tabs support-tabs" role="tablist" aria-label={t("Pedidos de suporte")}>
    {tabs.map(({id,label},index)=><button key={id} type="button" role="tab" id={`support-tab-${id}`} aria-controls={`support-panel-${id}`} aria-selected={active===id} tabIndex={active===id?0:-1} className={active===id?'selected':''} onClick={()=>setActive(id)} onKeyDown={event=>moveTab(event,index)}>
     {t(label)}{id==='confirmed'&&<small>{t(confirmed.length)}</small>}
    </button>)}
   </div>
   <section className="support-panel" id="support-panel-discord" role="tabpanel" aria-labelledby="support-tab-discord" hidden={active!=='discord'}>
    <div className="support-discord">
     <span className="connection-icon"><MessageSquarePlus size={24}/></span>
     <div><h2>{t('Peça um trainer no Discord')}</h2><p>{t(
      'Entre no servidor do ByteTrainer e envie o nome do jogo que você quer ver no catálogo.'
     )}</p>
     <button className="primary" disabled={!!busy} onClick={()=>action('open-discord',()=>api.openDiscord())}>{t('Abrir Discord ')}<ArrowUpRight size={16}/></button></div>
    </div>
   </section>
   <section className="support-panel" id="support-panel-confirmed" role="tabpanel" aria-labelledby="support-tab-confirmed" hidden={active!=='confirmed'} tabIndex={0}>
    <div className="support-roadmap-heading"><h2>{t('Suporte em breve')}</h2><p>{t(
     'Jogos confirmados pela equipe do ByteTrainer, ainda sem data de lançamento.'
    )}</p></div>
    {confirmed.length?<ul className="support-roadmap">
     {confirmed.map(game=><li key={game.id}><img src={covers[`./assets/confirmed/${game.id}.jpg`]} alt="" width="460" height="215" loading="lazy"/><h3>{t(game.name)}</h3></li>)}
    </ul>:<p>{t('Os jogos confirmados já estão disponíveis no catálogo.')}</p>}
   </section>
  </>
 );
}
