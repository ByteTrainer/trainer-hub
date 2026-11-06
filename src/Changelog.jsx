import React, {useEffect, useRef} from 'react';
import entries from '../assets/changelog.json';

export default function Changelog({version, open, onClose}) {
  const dialog = useRef(null);
  useEffect(() => {
    if (open && !dialog.current.open) dialog.current.showModal();
  }, [open]);
  return <dialog ref={dialog} className="changelog" aria-labelledby="changelog-title" onClose={onClose}>
    <header>
      <h2 id="changelog-title">Novidades do ByteTrainer</h2>
      <p>Versão instalada {version}</p>
    </header>
    <div className="changelog-entries">
      {entries.map(entry => <section key={entry.version} aria-label={`Versão ${entry.version}`}>
        <h3>v{entry.version}</h3>
        <ul>{entry.changes.map(change => <li key={change}>{change}</li>)}</ul>
      </section>)}
    </div>
    <form method="dialog"><button className="primary" autoFocus>Fechar</button></form>
  </dialog>;
}
