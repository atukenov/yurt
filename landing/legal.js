/* Language switch for the legal pages. Shares the saved choice with index.html (RU or EN). */
(function () {
  var lang = 'ru';
  try { lang = localStorage.getItem('yurt-landing-lang') === 'en' ? 'en' : 'ru'; } catch (e) {}
  function apply(l) {
    document.documentElement.lang = l;
    document.querySelectorAll('[data-l]').forEach(function (el) { el.hidden = el.dataset.l !== l; });
    document.querySelectorAll('.lang button').forEach(function (b) { b.setAttribute('aria-pressed', String(b.dataset.lang === l)); });
    var t = document.querySelector('title');
    if (t && t.dataset[l]) t.textContent = t.dataset[l];
    try { localStorage.setItem('yurt-landing-lang', l); } catch (e) {}
  }
  document.querySelectorAll('.lang button').forEach(function (b) { b.addEventListener('click', function () { apply(b.dataset.lang); }); });
  apply(lang);
})();
