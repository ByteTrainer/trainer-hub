import {t} from './i18n.js';
import React from 'react';import Link from 'lucide-react/icons/link';import Unplug from 'lucide-react/icons/unplug';import ChevronRight from 'lucide-react/icons/chevron-right';
export default function ConnectionCard({id,status,api,action,busy}){const d=status.diagnostic||{label:'Ready to connect?',message:'Connect the trainer to use the controls.'};return (
  <aside className="connection"><div className="connection-icon"><Link size={25}/></div><h2>{t(d.label)}</h2><p>{t(d.message)}</p><div className="connection-state"><span className={status.connected&&status.compatibility?.canConnect?'dot connected':'dot'}/>{t(status.owned?'Trainer connected to the app':'Trainer disconnected')}</div>{status.owned?<button className="secondary" disabled={!!busy} onClick={()=>action('disconnect',()=>api.disconnect(id))}><Unplug size={16}/>{t('Disconnect')}</button>:<button className="primary" disabled={!!busy||status.compatibility?.canConnect===false} onClick={()=>action('connect',()=>api.connect(id))}><Link size={16}/>{t('Connect trainer')}</button>}<div className="connection-tip"><p>{t(
      'Customize shortcuts and the overlay below. Only one trainer can be connected at a time.'
    )}</p></div><a className="text-button" href="#game-installation">{t('Installation folder')}<ChevronRight size={15}/></a></aside>
);}
