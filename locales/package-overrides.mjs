// Only a game's scope can be overridden; package text cannot rename app settings
// or another game's controls. Build fresh objects to invalidate translation caches.
export function withIntegrationTranslations(base,packages={}) {
 const scopes={...base.scopes};
 for(const [id,messages] of Object.entries(packages)){
  if(!/^\d+$/.test(id)||!messages||typeof messages!=='object')continue;
  const existing=base.scopes?.[id]||{};
  scopes[id]=Object.fromEntries([...new Set([...Object.keys(existing),...Object.keys(messages)])].map(locale=>[
   locale,{...(existing[locale]||base[locale]||base.en),...(messages[locale]||{})}
  ]));
 }
 return {...base,scopes};
}
