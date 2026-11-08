import {t} from './i18n.js';
import React, {useEffect, useRef} from 'react';
import entries from '../assets/changelog.json';

export default function Changelog({version, open, onClose}) {
  const dialog = useRef(null);
  useEffect(() => {
    if (open && !dialog.current.open) dialog.current.showModal();
  }, [open]);
  return (
    <dialog ref={dialog} className="changelog" aria-labelledby="changelog-title" onClose={onClose}>
      <header>
        <h2 id="changelog-title">{t('Novidades do ByteTrainer')}</h2>
        <p>{t('Versão instalada ')}{t(version)}</p>
      </header>
      <div className="changelog-entries">
        {entries.map(entry => <section key={entry.version} aria-label={t(`Versão ${entry.version}`)}>
          <h3>{t('v')}{t(entry.version)}</h3>
          <ul>{entry.changes.map(change => <li key={change}>{t(change)}</li>)}</ul>
        </section>)}
      </div>
      <form method="dialog"><button className="primary" autoFocus>{t('Fechar')}</button></form>
    </dialog>
  );
}
