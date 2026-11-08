import fs from 'node:fs/promises';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {load} from 'cheerio';
import {translate} from '../locales/translate.mjs';

const root = path.resolve(import.meta.dirname, '..');
const origin = 'https://bytetrainer.github.io/trainer-integrations/';

export async function localizeSite(site, filenames) {
  const languages = JSON.parse(await fs.readFile(path.join(root, 'locales/languages.json'), 'utf8'));
  const dictionaries = Object.fromEntries(await Promise.all(languages.map(async language => [language.code,
    JSON.parse(await fs.readFile(path.join(root, 'locales', language.code + '.json'), 'utf8'))
  ])));
  await fs.mkdir(path.join(site, 'locales'), {recursive: true});
  for (const file of ['languages.json', ...languages.map(language => language.code + '.json')]) {
    await fs.copyFile(path.join(root, 'locales', file), path.join(site, 'locales', file));
  }
  await fs.copyFile(path.join(root, 'src/assets/games/shake-header.jpg'), path.join(site, 'assets/shake-header.jpg'));
  const languageScript = await fs.readFile(path.join(root, 'scripts/site-language.js'));
  await fs.writeFile(path.join(site, 'assets/language.js'), languageScript);
  const version = createHash('sha256').update(languageScript).digest('hex').slice(0, 16);
  const route = (code, filename) => (code === 'pt-BR' ? '' : code + '/') + filename;
  for (const filename of filenames) {
    const source = await fs.readFile(path.join(site, filename), 'utf8');
    for (const language of languages) {
      const code = language.code;
      const $ = load(source);
      $('.header-actions').each((_, element) => {
        const button = $(element).children('.button').first();
        $(element).replaceWith(button);
      });
      $('.language-picker,script[data-language-script],link[data-language-alternate]').remove();
      $('.site-footer a[href$="/locales/README.md"]').remove();
      $('*').each((_, element) => {
        if (['script','style','svg','path'].includes(element.tagName)) return;
        for (const node of element.children || []) {
          if (node.type === 'text') node.data = translate(node.data, code, dictionaries);
        }
        for (const attribute of ['aria-label','alt','title','placeholder']) {
          const value = $(element).attr(attribute);
          if (value) $(element).attr(attribute, translate(value, code, dictionaries));
        }
      });
      $('meta[name=description]').attr('content', translate($('meta[name=description]').attr('content'), code, dictionaries));
      $('html').attr('lang', code);
      const output = route(code, filename);
      const directory = path.posix.dirname(output);
      if (code !== 'pt-BR') {
        $('[href],[src]').each((_, element) => {
          for (const attribute of ['href','src']) {
            const value = $(element).attr(attribute);
            if (!value || /^[a-z]+:|^\/\/|^#/i.test(value)) continue;
            const original = path.posix.normalize(path.posix.join(path.posix.dirname(filename), value.split('?')[0]));
            if (!original.startsWith('assets/') && !original.startsWith('packages/') && !original.startsWith('catalog-')) continue;
            const suffix = value.includes('?') ? '?' + value.split('?').slice(1).join('?') : '';
            $(element).attr(attribute, path.posix.relative(directory, original) + suffix);
          }
        });
      }
      $('link[rel=canonical]').attr('href', origin + output.replace(/index\.html$/, ''));
      const controls = $('<div class="header-actions"></div>');
      const picker = $('<label class="language-picker"><span></span><select id="site-language"></select></label>');
      picker.find('span').text(translate('Idioma', code, dictionaries));
      picker.find('select').attr('aria-label', translate('Idioma', code, dictionaries));
      for (const target of languages) {
        const targetRoute = route(target.code, filename);
        const relative = path.posix.relative(directory, targetRoute);
        $('<option></option>').val(target.code).text(target.name).attr('data-url', relative).prop('selected', target.code === code).appendTo(picker.find('select'));
        $('<link rel="alternate" data-language-alternate="true">').attr('hreflang', target.code).attr('href', origin + targetRoute.replace(/index\.html$/, '')).appendTo('head');
      }
      controls.append(picker);
      const download = $('.header-inner > .button').first();
      if (download.length) download.replaceWith(controls.append(download.clone()));
      else $('.header-inner').append(controls);
      const contribute = $('<a></a>').attr('href', 'https://github.com/ByteTrainer/trainer-hub/blob/main/locales/README.md').text(translate('Contribuir com traduções', code, dictionaries));
      $('.site-footer nav').append(contribute);
      const results = $('#catalog-results');
      results.attr('data-one', translate('jogo', code, dictionaries)).attr('data-many', translate('jogos', code, dictionaries));
      const prefix = path.posix.relative(directory, 'assets/language.js');
      $('<script defer data-language-script="true"></script>').attr('src', prefix + '?v=' + version).appendTo('head');
      const destination = path.join(site, output);
      await fs.mkdir(path.dirname(destination), {recursive: true});
      await fs.writeFile(destination, $.html());
    }
  }
}
