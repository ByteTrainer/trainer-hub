import {t, useLanguage} from './i18n.js';
import React, {useEffect, useState} from 'react';
import ArrowUp from 'lucide-react/icons/arrow-up';

const labels = { skull: 'Skull', vinyl: 'Vinyl' };

export default function PumpkinTracker() {
  useLanguage();
  const [targets, setTargets] = useState([]);
  useEffect(() => {
    document.documentElement.classList.add('pumpkin-hud-document');
    return window.pumpkinTracker.onTargets(setTargets);
  }, []);
  return (
    <section className="pumpkin-hud" aria-label={t("Nearest collectibles")}>
      {targets.map(({kind, distance, angle}) => <div className="pumpkin-target" key={kind}>
        <ArrowUp size={22} aria-hidden="true" style={{transform: `rotate(${angle}deg)`}}/>
        <div><span>{t(labels[kind])}</span><strong>{t(distance)}{t(' m')}</strong></div>
      </div>)}
    </section>
  );
}
