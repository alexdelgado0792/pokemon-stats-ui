const STAT_ABBREV = {
  'hp':'HP','attack':'Atk','defense':'Def',
  'special-attack':'SpA','special-defense':'SpD','speed':'Spe',
};

function natureLabel(n) {
  if (!n.increasedStat) return n.displayName;
  return `${n.displayName} (+${STAT_ABBREV[n.increasedStat]} / -${STAT_ABBREV[n.decreasedStat]})`;
}

async function loadNatures(select) {
  const res     = await fetch('/api/natures');
  const natures = await res.json();
  select.innerHTML = natures
    .map(n => `<option value="${n.name}">${natureLabel(n)}</option>`)
    .join('');
  // Default to Hardy
  const hardy = [...select.options].find(o => o.value === 'hardy');
  if (hardy) hardy.selected = true;
}

function debounce(fn, ms) {
  let timer;
  return (...args) => { clearTimeout(timer); timer = setTimeout(() => fn(...args), ms); };
}

function initPanel(panel) {
  const slot      = panel.dataset.slot;
  const search    = panel.querySelector('.search-input');
  const results   = panel.querySelector('.results-list');
  const natureEl  = panel.querySelector('.nature-select');
  const variantEl = panel.querySelector('.variant-select');
  const btnShow   = panel.querySelector('.btn-show');
  const btnClear  = panel.querySelector('.btn-clear');
  let selectedPokemon = null;

  loadNatures(natureEl);

  const doSearch = debounce(async (q) => {
    if (!q.trim()) { results.innerHTML = ''; return; }
    const res  = await fetch(`/api/pokemon/search?q=${encodeURIComponent(q)}&limit=8`);
    const list = await res.json();
    results.innerHTML = list.map(p =>
      `<li data-name="${p.name}">${p.displayName}</li>`
    ).join('');
    results.querySelectorAll('li').forEach(li => {
      li.addEventListener('click', () => {
        selectedPokemon = li.dataset.name;
        results.querySelectorAll('li').forEach(l => l.classList.remove('selected'));
        li.classList.add('selected');
        btnShow.disabled = false;
      });
    });
  }, 150);

  search.addEventListener('input', e => doSearch(e.target.value));

  btnShow.addEventListener('click', async () => {
    if (!selectedPokemon) return;
    await fetch('/api/overlay/set', {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({
        slot,
        pokemonName:   selectedPokemon,
        natureName:    natureEl.value,
        spriteVariant: variantEl.value,
      }),
    });
  });

  btnClear.addEventListener('click', async () => {
    await fetch('/api/overlay/clear', {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ slot }),
    });
  });
}

document.querySelectorAll('.panel').forEach(initPanel);

// Live indicators — connect to overlay WS to track slot state
function connectIndicator() {
  const ws       = new WebSocket(`ws://${location.host}/ws/overlay`);
  const indLeft  = document.getElementById('ind-left');
  const indRight = document.getElementById('ind-right');

  function setIndicator(slot, active) {
    const el = slot === 'left' ? indLeft : indRight;
    el.className = 'indicator' + (active ? ' active' : '');
  }

  ws.onmessage = (e) => {
    const msg = JSON.parse(e.data);
    if (msg.type === 'snapshot') {
      setIndicator('left',  !!msg.snapshot.left);
      setIndicator('right', !!msg.snapshot.right);
    } else if (msg.type === 'slotUpdate') {
      setIndicator(msg.slot, true);
    } else if (msg.type === 'slotClear') {
      setIndicator(msg.slot, false);
    }
  };

  ws.onclose = () => setTimeout(connectIndicator, 1500);
}

connectIndicator();
