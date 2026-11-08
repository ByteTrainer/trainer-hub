import {t} from './i18n.js';
import React from 'react';import Link from 'lucide-react/icons/link';import Unplug from 'lucide-react/icons/unplug';import ChevronRight from 'lucide-react/icons/chevron-right';
export default function ConnectionCard({id,status,api,action,busy}){const d=status.diagnostic||{label:'Vamos conectar?',message:'Conecte o trainer para usar os controles.'};return (
  <aside className="connection"><div className="connection-icon"><Link size={25}/></div><h2>{t(d.label)}</h2><p>{t(d.message)}</p><div className="connection-state"><span className={status.connected&&status.compatibility?.canConnect?'dot connected':'dot'}/>{t(status.owned?'Trainer conectado ao app':'Trainer desconectado')}</div>{status.owned?<button className="secondary" disabled={!!busy} onClick={()=>action('disconnect',()=>api.disconnect(id))}><Unplug size={16}/>{t('Desconectar')}</button>:<button className="primary" disabled={!!busy||status.compatibility?.canConnect===false} onClick={()=>action('connect',()=>api.connect(id))}><Link size={16}/>{t('Conectar trainer')}</button>}<div className="connection-tip"><p>{t(
      'Personalize seus atalhos e a sobreposição logo abaixo. Apenas um trainer fica conectado por vez.'
    )}</p></div><a className="text-button" href="#game-installation">{t('Pasta de instalação')}<ChevronRight size={15}/></a></aside>
);}
