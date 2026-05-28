// Maps PokeAPI stat identifiers (used in NatureData) to PokemonData.stats dict keys
const NATURE_TO_STAT_KEY = {
  'hp':'hp','attack':'attack','defense':'defense',
  'special-attack':'specialAttack','special-defense':'specialDefense','speed':'speed',
};

const STATS = [
  { key:'hp',             label:'HP'  },
  { key:'attack',         label:'Atk' },
  { key:'defense',        label:'Def' },
  { key:'specialAttack',  label:'SpA' },
  { key:'specialDefense', label:'SpD' },
  { key:'speed',          label:'Spe' },
];

const POKEBALL_SVG = `
<svg class="pokeball" viewBox="0 0 44 44" xmlns="http://www.w3.org/2000/svg">
  <path d="M22 2A20 20 0 0 1 42 22H2A20 20 0 0 1 22 2Z" fill="#e53935"/>
  <rect x="2" y="20" width="40" height="4" fill="white"/>
  <path d="M22 42A20 20 0 0 1 2 22H42A20 20 0 0 1 22 42Z" fill="white"/>
  <circle cx="22" cy="22" r="5" fill="white" stroke="#333" stroke-width="2"/>
</svg>`;

const pendingAnimations = { left: null, right: null };

function renderCard(payload) {
  if (!payload) return '<div class="empty">—</div>';
  const { pokemon, nature, spritePath } = payload;

  const boostedKey  = nature.increasedStat ? NATURE_TO_STAT_KEY[nature.increasedStat]  : null;
  const hinderedKey = nature.decreasedStat ? NATURE_TO_STAT_KEY[nature.decreasedStat] : null;

  const types = [pokemon.primaryType, pokemon.secondaryType].filter(Boolean);
  const typePills = types.map(t =>
    `<img class="type-badge" src="/data/types/type_${t}_square.png" alt="${t}">`
  ).join('');

  const natureRow = nature.increasedStat
    ? `${nature.displayName} <span class="nature-boost">+${nature.increasedStat}</span> / <span class="nature-hinder">-${nature.decreasedStat}</span>`
    : nature.displayName;

  const statRows = STATS.map(({ key, label }) => {
    const sv = pokemon.stats[key];
    if (!sv) return '';
    const cls = key === boostedKey  ? 'class="boosted"'
              : key === hinderedKey ? 'class="hindered"'
              : '';
    return `<tr ${cls}><td>${label}</td><td>${sv.min}</td><td>${sv.base}</td><td>${sv.max}</td></tr>`;
  }).join('');

  return `
    <img class="sprite" src="/data/sprites/${spritePath}" alt="${pokemon.displayName}">
    <div class="pokemon-name">${pokemon.displayName}</div>
    <div class="type-pills">${typePills}</div>
    <div class="nature-row">${natureRow}</div>
    <table class="stat-table">
      <thead><tr><th></th><th>Min</th><th>Base</th><th>Max</th></tr></thead>
      <tbody>${statRows}</tbody>
    </table>`;
}

function animateSlot(slotId, payload) {
  const card = document.getElementById(slotId === 'left' ? 'card-left' : 'card-right');

  if (pendingAnimations[slotId]) {
    clearTimeout(pendingAnimations[slotId]);
    pendingAnimations[slotId] = null;
  }

  card.classList.add('spinning');
  card.innerHTML = POKEBALL_SVG;

  pendingAnimations[slotId] = setTimeout(() => {
    pendingAnimations[slotId] = null;
    card.classList.remove('spinning');
    card.classList.add('fading-in');
    card.innerHTML = renderCard(payload);
    setTimeout(() => card.classList.remove('fading-in'), 250);
  }, 500);
}

function applySnapshot(snapshot) {
  for (const slot of ['left', 'right']) {
    const card = document.getElementById(`card-${slot}`);
    card.innerHTML = renderCard(snapshot[slot]);
  }
}

function connect() {
  const ws = new WebSocket(`ws://${location.host}/ws/overlay`);

  ws.onmessage = (e) => {
    const msg = JSON.parse(e.data);
    if (msg.type === 'snapshot')    applySnapshot(msg.snapshot);
    else if (msg.type === 'slotUpdate') animateSlot(msg.slot, msg.data);
    else if (msg.type === 'slotClear')  animateSlot(msg.slot, null);
  };

  ws.onclose = () => setTimeout(connect, 1500);
}

connect();
