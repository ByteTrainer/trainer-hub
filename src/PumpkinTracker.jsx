import React, {useEffect, useState} from 'react';
import ArrowUp from 'lucide-react/icons/arrow-up';

const labels = { skull: 'Caveira', vinyl: 'Vinil' };

export default function PumpkinTracker() {
  const [targets, setTargets] = useState([]);
  useEffect(() => {
    document.documentElement.classList.add('pumpkin-hud-document');
    return window.pumpkinTracker.onTargets(setTargets);
  }, []);
  return <section className="pumpkin-hud" aria-label="Colecionáveis mais próximos">
    {targets.map(({kind, distance, angle}) => <div className="pumpkin-target" key={kind}>
      <ArrowUp size={22} aria-hidden="true" style={{transform: `rotate(${angle}deg)`}}/>
      <div><span>{labels[kind]}</span><strong>{distance} m</strong></div>
    </div>)}
  </section>;
}
