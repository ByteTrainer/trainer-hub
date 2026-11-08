const languagePicker = document.querySelector('#site-language');
const languageStorageKey = 'bytetrainer.language';
if (languagePicker) {
  languagePicker.addEventListener('change', () => {
    const selected = languagePicker.selectedOptions[0];
    try { localStorage.setItem(languageStorageKey, selected.value); } catch {}
    location.assign(selected.dataset.url);
  });
  try {
    const preferred = localStorage.getItem(languageStorageKey);
    if (document.documentElement.lang === 'pt-BR' && preferred && preferred !== 'pt-BR') {
      const option = [...languagePicker.options].find(option => option.value === preferred);
      if (option) location.replace(option.dataset.url);
    }
  } catch {}
}
