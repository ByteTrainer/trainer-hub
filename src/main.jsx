import {applyTheme,useSystemTheme} from './theme.js';
import CheatProfiles from './CheatProfiles.jsx';
import {t, getLocale, useLanguage, setLanguage, setIntegrationTranslations, languages} from './i18n.js';
import shakeCover from './assets/games/shake-cover.jpg';
import shakeHeader from './assets/games/shake-header.jpg';
import React,{useState,useEffect,useMemo,useRef} from 'react';import{createRoot}from'react-dom/client';
import Gamepad2 from 'lucide-react/icons/gamepad-2';
import Library from 'lucide-react/icons/library';
import Search from 'lucide-react/icons/search';
import Star from 'lucide-react/icons/star';
import ArrowUpRight from 'lucide-react/icons/arrow-up-right';
import ArrowLeft from 'lucide-react/icons/arrow-left';
import ChevronRight from 'lucide-react/icons/chevron-right';
import ChevronLeft from 'lucide-react/icons/chevron-left';
import SlidersHorizontal from 'lucide-react/icons/sliders-horizontal';
import RefreshCw from 'lucide-react/icons/refresh-cw';
import Settings from 'lucide-react/icons/settings';
import MessageSquarePlus from 'lucide-react/icons/message-square-plus';
import Activity from 'lucide-react/icons/activity';
import Play from 'lucide-react/icons/play';
import Link from 'lucide-react/icons/link';
import Unplug from 'lucide-react/icons/unplug';
import FolderOpen from 'lucide-react/icons/folder-open';
import Download from 'lucide-react/icons/download';
import RotateCcw from 'lucide-react/icons/rotate-ccw';
import Minus from 'lucide-react/icons/minus';
import Square from 'lucide-react/icons/square';
import X from 'lucide-react/icons/x';
import Command from 'lucide-react/icons/command';
import Zap from 'lucide-react/icons/zap';
import Monitor from 'lucide-react/icons/monitor';
import CheckCircle2 from 'lucide-react/icons/circle-check';
import Heart from 'lucide-react/icons/heart';
import Focus from 'lucide-react/icons/focus';
import Crosshair from 'lucide-react/icons/crosshair';
import InfinityIcon from 'lucide-react/icons/infinity';
import RotateCw from 'lucide-react/icons/rotate-cw';
import Flame from 'lucide-react/icons/flame';
import AlertCircle from 'lucide-react/icons/circle-exclamation-point';
import Trophy from 'lucide-react/icons/trophy';
import Timer from 'lucide-react/icons/timer';
import SolTrainer from './SolTrainer.jsx';
import TrainerControls,{useInputFocus} from './TrainerControls.jsx';
import HotkeySettings from './HotkeySettings.jsx';
import Overlay from './Overlay.jsx';
import Changelog from './Changelog.jsx';
import PumpkinTracker from './PumpkinTracker.jsx';
import IntegrationPanel from './IntegrationPanel.jsx';
import GameInstallation from './GameInstallation.jsx';
import SupportRequests from './SupportRequests.jsx';
import IntegrationUpdates from './IntegrationUpdates.jsx';
import ConnectionCard from './ConnectionCard.jsx';
import commands from '../electron/commands.json';
import pedroOptions from '../electron/pedro-options.json';
import {SOUND_STYLES,createSoundPlayer} from './audio.js';
import '@fontsource-variable/manrope';import '@fontsource-variable/space-grotesk';import './style.css';import './integrations.css';
const api=window.hub;const icons=[Heart,Focus,Crosshair,InfinityIcon,RotateCw,Flame,Trophy,Timer];
const cheats=pedroOptions.options.map(option=>[option.name,option.description]);
function Art({game,kind='cover',...props}){const[fallback,setFallback]=useState(false);return <img {...props} src={game.id===3578070?(kind==='cover'?shakeCover:shakeHeader):(fallback?game.header:game[kind]||game.header)} alt="" loading="lazy" decoding="async" onError={e=>{if(!fallback&&game.header){setFallback(true);}else{e.currentTarget.style.visibility='hidden';}}}/>;}
function SoundSettings({settings,onChange,onPreview}){
 return (
  <section className="settings-section sound-settings"><h2>{t('Sound feedback')}</h2><p id="sound-help">{t(
    'Different sounds for enabling and disabling cheats through buttons or shortcuts. Your choice is saved on this computer.'
   )}</p>
   <div className="sound-enabled"><label htmlFor="sound-enabled">{t('Enable cheat sounds')}</label><input id="sound-enabled" aria-describedby="sound-help" className="switch" type="checkbox" role="switch" checked={settings.enabled} onChange={e=>onChange({enabled:e.target.checked})}/></div>
   <div className="sound-volume"><label htmlFor="sound-volume">{t('Volume')}</label><input id="sound-volume" type="range" min="0" max="100" step="1" value={settings.volume} aria-valuetext={t(`${settings.volume}%`)} onChange={e=>onChange({volume:Number(e.target.value)})}/><output htmlFor="sound-volume">{t(settings.volume)}{t('%')}</output></div>
   <fieldset className="sound-styles"><legend>{t('Sound style')}</legend><div>{SOUND_STYLES.map(style=><label key={style.id} className={settings.style===style.id?'sound-variant selected':'sound-variant'}><input type="radio" name="sound-style" value={style.id} checked={settings.style===style.id} onChange={()=>onChange({style:style.id})}/><span><strong>{t(style.name)}</strong><small>{t(style.description)}</small></span></label>)}</div></fieldset>
   <div className="button-row"><button className="secondary" disabled={!settings.enabled||settings.volume===0} onClick={()=>onPreview('activate')}><Play size={15}/>{t('Preview enable sound')}</button><button className="secondary" disabled={!settings.enabled||settings.volume===0} onClick={()=>onPreview('deactivate')}><Play size={15}/>{t('Preview disable sound')}</button></div><small>{t(
    !settings.enabled?'Sound effects are disabled.':settings.volume===0?'Volume is zero. Sound effects are silent.':'Previews play the sound only and do not change trainer options.'
   )}</small>
  </section>
 );
}
function App(){const locale=useLanguage(),systemTheme=useSystemTheme();useInputFocus(api);const[data,setData]=useState(null),[page,setPage]=useState('catalog'),[selected,setSelected]=useState(null),[query,setQuery]=useState(''),[filter,setFilter]=useState('all'),[sort,setSort]=useState('name'),[offset,setOffset]=useState(0),[busy,setBusy]=useState(''),[toast,setToast]=useState(null),[combo,setCombo]=useState('10');const [showChangelog,setShowChangelog]=useState(false),[updateStatus,setUpdateStatus]=useState('');const searchRef=useRef(null),contentRef=useRef(null);const PAGE_SIZE=36;
 useEffect(()=>{if(data?.theme)applyTheme(data.theme);},[data?.theme]);
 useEffect(()=>{if(data?.locale)setLanguage(data.locale);},[data?.locale]);
 useEffect(()=>{if(data?.integrationTranslations)setIntegrationTranslations(data.integrationTranslations);},[data?.translationRevision]);
 const soundPlayer=useRef(null),soundErrorShown=useRef(false);if(!soundPlayer.current)soundPlayer.current=createSoundPlayer();
 async function action(name,fn){if(busy)return;setBusy(name);try{const value=await fn();return value;}catch(e){setToast({error:true,text:e.message.replace(/^Error invoking remote method '[^']+': Error: /,'')});}finally{setBusy('');}}
 useEffect(()=>{if(!api){setToast({error:true,text:'Open the Windows app to access your library.'});return;}api.snapshot().then(value=>{setData(value);setShowChangelog(!!value.showChangelog);}).catch(e=>setToast({error:true,text:e.message}));return api.onStatus(status=>setData(old=>old?{...old,...status}:old));},[]);
 useEffect(()=>{if(!api)return;const offCue=api.onSound(({kind})=>{soundPlayer.current.play(kind).catch(()=>{if(!soundErrorShown.current){soundErrorShown.current=true;setToast({error:true,text:'Could not play the sound. Check your Windows audio output.'});}});});const offSettings=api.onSoundSettings(sound=>{soundPlayer.current.configure(sound);setData(old=>old?{...old,sound}:old);});return()=>{offCue();offSettings();soundPlayer.current.dispose();};},[]);
 useEffect(()=>{const off=api.onPreferences(value=>setData(old=>old?{...old,...value}:old));const error=api.onTrainerError(text=>setToast({text,error:true}));return()=>{off();error();};},[]);
 useEffect(()=>{if(data?.sound)soundPlayer.current.configure(data.sound);},[data?.sound]);
 async function changeSound(patch){const before=data.sound;const next={...before,...patch};soundPlayer.current.configure(next);setData(old=>({...old,sound:next}));try{await api.setSound(patch);}catch(e){soundPlayer.current.configure(before);setData(old=>({...old,sound:before}));setToast({error:true,text:e.message});}}
 const previewSound=kind=>api.previewSound(kind).catch(()=>setToast({error:true,text:'Could not play the preview.'}));
 useEffect(()=>{if(data?.pedro?.combo)setCombo(String(data.pedro.combo));},[data?.pedro?.combo]);
 useEffect(()=>{setOffset(0);},[query,filter,page,sort]);useEffect(()=>{contentRef.current?.scrollTo({top:0});},[page,selected,offset]);
 useEffect(()=>{const listener=e=>{if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='k'){e.preventDefault();setSelected(null);setPage('catalog');requestAnimationFrame(()=>searchRef.current?.focus());}if(e.key==='Escape'&&selected)setSelected(null);};window.addEventListener('keydown',listener);return()=>window.removeEventListener('keydown',listener);},[selected]);
 useEffect(()=>{if(!toast)return;const t=setTimeout(()=>setToast(null),6500);return()=>clearTimeout(t);},[toast]);
 const games=data?.games||[];const supported=games;const installed=games.filter(g=>g.installed);const game=games.find(g=>g.id===selected);const pedro=data?.pedro;const trainerStatus=data?.managed?.[game?.id]|| (game?.id===1186640?data.pumpkin:game?.id===2738490?data.solcesto:pedro);const activeCheats=trainerStatus?.options.filter(Boolean).length??0;const currentPage={catalog:'Discover your next game.',library:'Your library.',favorites:'Your favorites.',requests:'What\'s next.',activity:'Activity.',settings:'Settings.'}[page];
 const filtered=useMemo(()=>games.filter(g=>(!query||g.name.toLocaleLowerCase(getLocale()).includes(query.toLocaleLowerCase(getLocale())))&&(page!=='library'||g.installed)&&(page!=='favorites'||g.favorite)&&(filter!=='installed'||g.installed)).sort((a,b)=>(sort==='installed'?Number(b.installed)-Number(a.installed):0)||a.name.localeCompare(b.name,getLocale())),[games,query,page,filter,sort,locale]);
 async function updateGames(method,id){await action(method,async()=>{const list=await api[method](id);setData(old=>({...old,games:list}));});}
 function navigate(target){setPage(target);setSelected(null);setQuery('');setFilter('all');}
 const scan=()=>action('scan',async()=>{setData(await api.scan());setToast({text:'Steam library checked.'});});
 const locate=(id,automatic)=>action('game-folder',async()=>setData(await api.locate(id,automatic)));
 const launch=id=>action('launch',async()=>{await api.launch(id);setToast({text:'Launch requested through Steam.'});});
 if(!data)return <div className="loading"><Gamepad2 size={40}/><h1>{t('ByteTrainer')}</h1><p>{t(toast?.text||'Preparing your library…')}</p></div>;
 return (
  <div className="app-shell"><a className="skip" href="#content">{t('Skip to content')}</a>
   <aside className="sidebar"><div className="brand"><span className="brand-mark"><Gamepad2 size={24}/></span><span>{t('Byte')}<span className="brand-light">{t('Trainer')}</span><small>{t('DESKTOP')}</small></span></div><nav aria-label={t("Main navigation")}>{[[Library,'catalog','Catalog',games.length],[Monitor,'library','My games',installed.length],[Star,'favorites','Favorites',games.filter(g=>g.favorite).length]].map(([Icon,id,label,count])=><button key={id} className={page===id?'nav-item active':'nav-item'} onClick={()=>navigate(id)} aria-current={page===id?'page':undefined}><Icon size={19}/><span>{t(label)}</span><small>{t(count)}</small></button>)}<div className="nav-divider"/>{[[MessageSquarePlus,'requests','Support requests',null],[Activity,'activity','Activity',null]].map(([Icon,id,label,count])=><button key={id} className={page===id?'nav-item active':'nav-item'} onClick={()=>navigate(id)}><Icon size={19}/><span>{t(label)}</span>{count!==null&&<small>{t(count)}</small>}</button>)}</nav><div className="sidebar-bottom"><div className="growth"><Zap size={20}/><strong>{t('Which game is next?')}</strong><p>{t('See confirmed games and share suggestions on Discord.')}</p><button onClick={()=>navigate('requests')}>{t('View support requests ')}<ChevronRight size={15}/></button></div><button className={page==='settings'?'nav-item active':'nav-item'} onClick={()=>navigate('settings')}><Settings size={18}/><span>{t('Settings')}</span></button><div className="local-profile"><span>{t('BT')}</span><div><strong>{t('Your space')}</strong><small>{t('Data on this computer')}</small></div><span className="online-dot"/></div></div></aside>
   <div className="workspace"><header className="topbar"><div className="breadcrumbs"><span>{t(game?'Library':'Your space')}</span><ChevronRight size={13}/><strong>{t(
    game?.name||{catalog:'Catalog',library:'My games',favorites:'Favorites',requests:'Support requests',activity:'Activity',settings:'Settings'}[page]
   )}</strong></div><div className="window-tools"><button className="version" aria-label={t(`What's new in version ${data.version}`)} aria-haspopup="dialog" onClick={()=>setShowChangelog(true)}>{t('v')}{t(data.version)}</button><button aria-label={t("Minimizar")} onClick={()=>api.window('minimize')}><Minus size={16}/></button><button aria-label={t("Maximizar ou restaurar")} onClick={()=>api.window('maximize')}><Square size={13}/></button><button className="window-close" aria-label={t("Fechar aplicativo")} onClick={()=>api.window('close')}><X size={17}/></button></div></header>
   <main id="content" ref={contentRef} tabIndex="-1">
   {!game&&page==='requests'&&<SupportRequests games={games} api={api} action={action} busy={busy}/>}
   {!game&&['catalog','library','favorites'].includes(page)&&<><div className="page-heading"><div><h1>{t(currentPage)}</h1><p>{t(
    page==='catalog'?'Explore games with available trainers.':page==='library'?'Games with trainers found in your Steam folders.':'Your favorite games, always close by.'
   )}</p></div><button className="secondary" onClick={scan} disabled={!!busy}><RefreshCw size={15} className={busy==='scan'?'spin':''}/>{t('Detect games')}</button></div>
   {page==='catalog'&&!query&&filter==='all'&&games.some(game=>game.id===557340)&&<section className="feature" aria-label={t("Trainer available for My Friend Pedro")}><Art game={{...games.find(game=>game.id===557340),hero:"https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/557340/library_hero.jpg"}} kind="hero"/><div className="feature-shade"/><div className="feature-content"><h2>{t('My Friend')}<br/>{t('Pedro')}<span>{t('.')}</span></h2><p>{t('Health, focus, ammo and much more.')}<br/>{t('Eight ways to play your way.')}</p><button className="primary" onClick={()=>setSelected(557340)}><Play size={16} fill="currentColor"/>{t('Open trainer')}<ArrowUpRight size={16}/></button></div><span className="feature-corner">{t('{0} options',{0:8})}<span>{t('/')}</span>{t(' Steam')}</span></section>}
   <div className="catalog-toolbar"><div className="catalog-tabs">{[['all','All'],['installed','Installed']].map(([id,label])=><button key={id} className={filter===id?'selected':''} onClick={()=>setFilter(id)} aria-pressed={filter===id}>{t(label)}</button>)}</div><label className="search"><Search size={17}/><span className="sr-only">{t('Search games')}</span><input ref={searchRef} type="search" placeholder={t("Search for a game…")} value={query} onChange={e=>setQuery(e.target.value)}/><kbd>{t('Ctrl K')}</kbd></label><label className="sort"><SlidersHorizontal size={16}/><span className="sr-only">{t('Sort games')}</span><select value={sort} onChange={e=>setSort(e.target.value)}><option value="name">{t('Name A–Z')}</option><option value="installed">{t('Installed first')}</option></select></label></div>
   <div className="results-line"><span>{t(filtered.length)} {t(filtered.length===1?'game':'games')}{t(query&&`for “${query}”`)}</span></div>
   {filtered.length?<ul className="game-grid">{filtered.slice(offset,offset+PAGE_SIZE).map(g=><li key={g.id}><div className="game-card"><button className="card-main" onClick={()=>setSelected(g.id)} aria-label={t(`Open ${g.name}, trainer available`)}><div className="poster"><Art game={g}/><span className="poster-fallback">{t(g.name)}</span><span className="poster-badge"><Zap size={12} fill="currentColor"/>{t((g.commands||commands[g.id]).length)}{t(' options')}</span><span className="poster-open"><ArrowUpRight size={24}/></span></div><h2>{t(g.name)}</h2><span className="game-meta supported">{t(g.installed?'Installed':'Trainer available')}</span></button><button className={g.favorite?'favorite starred':'favorite'} aria-label={t(
    `${g.favorite?'Remove':'Add'} ${g.name} ${g.favorite?'from':'to'} favorites`
   )} aria-pressed={g.favorite} onClick={()=>updateGames('favorite',g.id)}><Star size={15} fill={g.favorite?'currentColor':'none'}/></button></div></li>)}</ul>:<div className="empty"><Search size={32}/><h2>{t(
    query?'We couldn\'t find that game.':page==='favorites'?'Your collection starts with a favorite.':'No games match this filter.'
   )}</h2><p>{t(
    query?'You can suggest this game on Discord.':page==='favorites'?'Use the star on a game\'s cover to save it here.':'Browse the catalog or detect your installed games.'
   )}</p>{query?<button className="primary" onClick={()=>navigate('requests')}>{t('Support requests')}</button>:<button className="secondary" onClick={()=>navigate('catalog')}>{t('Browse catalog')}</button>}</div>}
   {filtered.length>PAGE_SIZE&&<div className="pagination"><span>{t(offset+1)}{t('–')}{t(Math.min(offset+PAGE_SIZE,filtered.length))}{t(' of ')}{t(filtered.length)}</span><button className="secondary" disabled={offset===0} onClick={()=>setOffset(offset-PAGE_SIZE)}><ChevronLeft size={16}/>{t('Previous')}</button><button className="secondary" disabled={offset+PAGE_SIZE>=filtered.length} onClick={()=>setOffset(offset+PAGE_SIZE)}>{t('Next')}<ChevronRight size={16}/></button></div>}</>}
   {game&&<><button className="back" onClick={()=>setSelected(null)}><ArrowLeft size={16}/>{t('Back to library')}</button><section className="game-header"><div className="game-header-art"><Art game={game} kind="header"/></div><div className="game-title"><span className="status supported"><Zap size={13}/>{t(' Trainer available')}</span><h1>{t(game.name)}</h1><p>{t('Steam ')}<span>{t('·')}</span> {t(
    (game.installed||trainerStatus?.path&&game.supported)?'Installed on this computer':'Not detected on this computer'
   )}{t(
    game.supported&&`· ${(game.commands||commands[game.id]).length} trainer options`
   )}</p><div className="game-actions"><button className="primary" disabled={!!busy} onClick={()=>launch(game.id)}><Play size={16} fill="currentColor"/>{t(busy==='launch'?'Launching…':'Launch through Steam')}</button><button className="secondary" aria-pressed={game.favorite} onClick={()=>updateGames('favorite',game.id)}><Star size={16} fill={game.favorite?'currentColor':'none'}/>{t(game.favorite?'Favorited':'Favorite')}</button></div></div></section>
   {game.supported&&<IntegrationPanel id={game.id} status={trainerStatus} online={!!data.integrationUpdates.url} remote={data.integrationUpdates.available.find(entry=>entry.gameId===game.id)} api={api} action={action} busy={busy} notify={text=>setToast({text})}/>}
   <CheatProfiles gameId={game.id} entry={data.cheatProfiles?.[game.id]} status={trainerStatus} api={api} action={action} busy={busy}/>{game.id===2738490?<SolTrainer status={data.solcesto} bindings={data.hotkeys[2738490]} parameters={data.solParameters} action={action} api={api} busy={busy} navigate={navigate} notify={text=>setToast({text})}/>:<div className="trainer-layout"><section className="trainer-panel"><div className="section-title"><h2>{t('Your game, your rules.')}</h2><span>{t(activeCheats)} {t(activeCheats===1?"active":"active")}</span></div><p className="section-copy">{t('Enable an option here or use its shortcut while playing.')}</p><TrainerControls gameId={game.id} status={trainerStatus} bindings={data.hotkeys[game.id]} api={api} action={action} busy={busy}/></section><ConnectionCard id={game.id} status={trainerStatus} api={api} action={action} busy={busy} navigate={navigate}/></div>}{game.supported&&<GameInstallation game={game} status={trainerStatus} customPath={data.gamePaths?.[game.id]} onLocate={locate} busy={busy}/>} {game.supported&&<HotkeySettings key={game.id} id={game.id} bindings={data.hotkeys[game.id]} enabled={!!data.overlayPreferences[game.id]?.enabled} status={trainerStatus} api={api} action={action} busy={busy} notify={text=>setToast({text,error:true})}/>}</>}
   {!game&&page==='activity'&&<><div className="page-heading"><div><h1>{t('Activity.')}</h1><p>{t('The latest actions on this computer.')}</p></div><button className="secondary" onClick={()=>action('activity',async()=>setData(await api.snapshot()))}><RefreshCw size={16}/>{t('Update')}</button></div>{data.events.length?<ol className="activity-list">{data.events.map((event,i)=><li key={i}><span><Activity size={17}/></span><div><p>{t(event.message)}</p><time dateTime={event.time}>{t(new Date(event.time).toLocaleString(getLocale()))}</time></div></li>)}</ol>:<div className="empty"><Activity size={32}/><h2>{t('Ready to get started.')}</h2><p>{t('Trainer connections, detection and launches will appear here.')}</p></div>}</>}
   {!game&&page==='settings'&&<><div className="page-heading"><div><h1>{t('Make it yours.')}</h1><p>{t('Library, integrations and app data.')}</p></div></div><section className="settings-section"><h2>{t('ByteTrainer updates')}</h2><p>{t('Installed version v')}{t(data.version)}</p><button className="secondary" disabled={!!busy} onClick={()=>action('app-update',async()=>setUpdateStatus(await api.checkAppUpdates()))}><RefreshCw size={16}/>{t(busy==='app-update'?'Checking…':'Check for app updates')}</button><p role="status">{t(updateStatus)}</p></section><section className="settings-section"><h2>{t("App language")}</h2><label className="language-setting" htmlFor="app-language"><span>{t("Language")}</span><select id="app-language" value={locale} disabled={!!busy} onChange={event=>action("language",async()=>{const selected=event.target.value;await api.setLanguage(selected);setLanguage(selected);})}>{languages.map(language=><option key={language.code} value={language.code}>{language.name}</option>)}</select></label><p>{t("Your choice is saved on this computer and also applies to the overlay.")}</p><a className="text-button" href="https://github.com/ByteTrainer/trainer-hub/blob/main/locales/README.md" onClick={event=>{event.preventDefault();api.openTranslations();}}>{t("Contribute translations")}</a></section><section className="settings-section"><h2>{t('Appearance')}</h2><label className="language-setting" htmlFor="app-theme"><span>{t('Theme')}</span><select id="app-theme" value={data.theme||'system'} disabled={!!busy} onChange={event=>action('theme',async()=>{const theme=await api.setTheme(event.target.value);applyTheme(theme);setData(old=>({...old,theme}));})}><option value="system">{t('Follow system')} — {t(systemTheme==='dark'?'Dark':'Light')}</option><option value="light">{t('Light')}</option><option value="dark">{t('Dark')}</option></select></label><p>{t('Follow the Windows appearance automatically, or choose a theme for the app and overlay.')}</p></section><SoundSettings settings={data.sound} onChange={changeSound} onPreview={previewSound}/><section className="settings-section"><h2>{t('Steam library')}</h2><p>{t(data.steamRoot)}</p><button className="secondary" onClick={scan} disabled={!!busy}><RefreshCw size={16}/>{t('Detect games again')}</button></section><IntegrationUpdates openGame={setSelected} supported={supported.map(game=>game.id)} state={data.integrationUpdates} api={api} action={action} busy={busy} notify={text=>setToast({text})}/><section className="settings-section"><h2>{t('Catalog and preferences')}</h2><p>{t(games.length)}{t(' games with available trainers.')}</p><div className="button-row"><button className="secondary" onClick={()=>api.openData()}><FolderOpen size={16}/>{t('Open data folder')}</button></div><small>{t('Favorites are saved on this computer.')}</small></section><section className="settings-section about"><span className="brand-mark"><Gamepad2 size={24}/></span><div><h2>{t('ByteTrainer ')}<small>{t('v')}{t(data.version)}</small></h2><p>{t('Trainers for Steam games on Windows.')}</p></div></section></>}
   </main><footer className="bottom-bar"><span><span className="dot connected"/>{t(
    (pedro.owned||data.solcesto.owned||data.pumpkin.owned||Object.values(data.managed||{}).some(status=>status.owned))?'Trainer connected to the app':'Local library'
   )} </span><span>{t(supported.length)}{' '}{t(supported.length===1?'game with a trainer':'games with trainers')}{' '}<span>{t('·')}</span>{t(' Windows ')}<span>{t('·')}</span>{' '}{languages.find(language=>language.code===locale)?.name}</span></footer></div>
   <Changelog version={data.version} open={showChangelog} onClose={()=>{setShowChangelog(false);api.markChangelogSeen().catch(e=>setToast({error:true,text:e.message}));}}/>
   {toast&&<div className={toast.error?'toast error':'toast'} role={toast.error?'alert':'status'}>{toast.error?<AlertCircle size={20}/>:<CheckCircle2 size={20}/>}<span>{t(toast.text)}</span><button aria-label={t("Dismiss message")} onClick={()=>setToast(null)}><X size={16}/></button></div>}</div>
 );
}
const view = new URLSearchParams(location.search);
createRoot(document.getElementById('root')).render(view.has('pumpkinTracker')?<PumpkinTracker/>:view.has('overlay')?<Overlay/>:<App/>);
