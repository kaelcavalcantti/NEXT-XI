// A CASA QUE RESPIRA — Parte 1: A Herança
// Fonte única de verdade para tempo: planos (shots), falas e eventos sonoros/visuais.
// O renderizador (JS) e o motor de áudio (Python, via build/timeline.json) leem este arquivo.

export const FPS = 24;
export const WIDTH = 1920;
export const HEIGHT = 1080;

// Cada plano: id, duração (s). Os tempos absolutos são calculados abaixo.
export const SHOTS = [
  { id: 'aerial_town',   dur: 13 },  // sobrevoo da cidade sob chuva
  { id: 'road_to_house', dur: 10 },  // estrada entre árvores até a casa
  { id: 'window_push',   dur: 11 },  // aproximação da janela, silhueta
  { id: 'title',         dur: 8 },   // título
  { id: 'apartment',     dur: 24 },  // a ligação da herança
  { id: 'car_exterior',  dur: 7 },   // carro na estrada
  { id: 'car_interior',  dur: 19 },  // rádio sussurra
  { id: 'arrival',       dur: 14 },  // chegada à casa
  { id: 'front_door',    dur: 11 },  // chave antiga, porta range
  { id: 'hallway',       dur: 23 },  // a casa respira
  { id: 'album',         dur: 27 },  // as fotografias
  { id: 'clock',         dur: 10 },  // 23h
  { id: 'stairs',        dur: 13 },  // subida
  { id: 'child_room',    dur: 32 },  // o quarto e as cartas
  { id: 'knocks',        dur: 13 },  // três batidas, "Gabriel..."
  { id: 'basement_door', dur: 34 },  // a porta do porão
  { id: 'run_phone',     dur: 31 },  // o telefonema de Augusto
  { id: 'double_reveal', dur: 18 },  // o duplo
  { id: 'decision',      dur: 15 },  // a decisão
  { id: 'black_whisper', dur: 8 },   // "Agora podemos começar."
  { id: 'choice_card',   dur: 17 },  // VOCÊ DECIDE
  { id: 'end_card',      dur: 10 },  // FIM DA PARTE 1
];

let acc = 0;
for (const s of SHOTS) { s.start = acc; acc += s.dur; s.end = acc; }
export const TOTAL = acc;
export const shotById = Object.fromEntries(SHOTS.map(s => [s.id, s]));
export const T = (shot, off) => shotById[shot].start + off;

// Vozes (Kokoro-82M, vozes PT-BR): pm_alex (Gabriel), pm_santa (Augusto),
// pf_dora (a voz feminina). 'mix' cria timbres distintos por mistura de estilos.
// fx: cadeia de processamento aplicada no motor de áudio.
export const LINES = [
  { id: 'L1', who: 'advogado', voice: { pm_santa: 0.55, pm_alex: 0.45 }, speed: 1.0, pitch: -1, fx: 'phone',
    shot: 'apartment', at: 4.6, text: 'Gabriel Moreira? Aqui é o doutor Henrique, do cartório de Pedra Funda.' },
  { id: 'L2', who: 'advogado', voice: { pm_santa: 0.55, pm_alex: 0.45 }, speed: 0.97, pitch: -1, fx: 'phone',
    shot: 'apartment', at: 9.6, text: 'Seu avô, Augusto, deixou uma propriedade no seu nome.' },
  { id: 'G1', who: 'gabriel', voice: { pm_alex: 1 }, speed: 0.9, pitch: 0, fx: 'dry_room',
    shot: 'apartment', at: 14.2, text: 'Meu avô? Eu nunca conheci o meu avô.' },
  { id: 'L3', who: 'advogado', voice: { pm_santa: 0.55, pm_alex: 0.45 }, speed: 0.95, pitch: -1, fx: 'phone',
    shot: 'apartment', at: 18.0, text: 'Ele desapareceu há trinta e dois anos. A chave está com os documentos.' },

  { id: 'R1', who: 'ela', voice: { pf_dora: 1 }, speed: 0.78, pitch: -2, fx: 'radio_whisper',
    shot: 'car_interior', at: 8.4, text: 'Você demorou para voltar.' },

  { id: 'G2', who: 'gabriel', voice: { pm_alex: 1 }, speed: 0.82, pitch: 0, fx: 'house',
    shot: 'album', at: 19.2, text: 'Mil novecentos e noventa e quatro... Eu nem tinha nascido.' },

  { id: 'A1', who: 'augusto_carta', voice: { pm_santa: 1 }, speed: 0.86, pitch: -2, fx: 'letter',
    shot: 'child_room', at: 12.0, text: 'Se você está lendo isso, significa que ela finalmente encontrou você.' },
  { id: 'A2', who: 'augusto_carta', voice: { pm_santa: 1 }, speed: 0.86, pitch: -2, fx: 'letter',
    shot: 'child_room', at: 18.6, text: 'Não confie nas vozes. Não responda quando ouvir seu nome.' },
  { id: 'A3', who: 'augusto_carta', voice: { pm_santa: 1 }, speed: 0.8, pitch: -2, fx: 'letter',
    shot: 'child_room', at: 24.8, text: 'E, acima de tudo... nunca abra a porta do porão.' },

  { id: 'D1', who: 'duplo', voice: { pm_alex: 1 }, speed: 0.7, pitch: -3, fx: 'double_whisper',
    shot: 'knocks', at: 5.2, text: 'Gabriel...' },

  { id: 'F1', who: 'ela', voice: { pf_dora: 1 }, speed: 0.72, pitch: -2, fx: 'behind_door',
    shot: 'basement_door', at: 12.0, text: 'Meu filho...' },
  { id: 'F2', who: 'ela', voice: { pf_dora: 1 }, speed: 0.78, pitch: -2, fx: 'behind_door',
    shot: 'basement_door', at: 16.0, text: 'Eu sei que você está aí.' },
  { id: 'G3', who: 'gabriel', voice: { pm_alex: 1 }, speed: 0.85, pitch: 0, fx: 'house',
    shot: 'basement_door', at: 20.6, text: 'Quem está aí?' },
  { id: 'F3', who: 'ela', voice: { pf_dora: 1 }, speed: 0.7, pitch: -3, fx: 'behind_door',
    shot: 'basement_door', at: 25.6, text: 'Você não deveria ter nascido.' },

  { id: 'G4', who: 'gabriel', voice: { pm_alex: 1 }, speed: 0.9, pitch: 0, fx: 'house',
    shot: 'run_phone', at: 7.4, text: 'Alô?' },
  { id: 'A4', who: 'augusto', voice: { pm_santa: 1 }, speed: 0.9, pitch: -2, fx: 'phone_bad',
    shot: 'run_phone', at: 8.8, text: 'Gabriel, escute com atenção. Eu sou Augusto. Seu avô.' },
  { id: 'A5', who: 'augusto', voice: { pm_santa: 1 }, speed: 0.98, pitch: -2, fx: 'phone_bad',
    shot: 'run_phone', at: 17.0, text: 'Você precisa sair dessa casa agora.' },
  { id: 'G5', who: 'gabriel', voice: { pm_alex: 1 }, speed: 0.92, pitch: 0, fx: 'house',
    shot: 'run_phone', at: 20.6, text: 'Como você sabe que estou aqui?' },
  { id: 'A6', who: 'augusto', voice: { pm_santa: 1 }, speed: 0.84, pitch: -2, fx: 'phone_bad',
    shot: 'run_phone', at: 24.0, text: 'Porque eu estou no andar de cima.' },

  { id: 'A7', who: 'augusto', voice: { pm_santa: 1 }, speed: 1.0, pitch: -2, fx: 'phone_scream',
    shot: 'double_reveal', at: 4.6, text: 'Não olhe para ele!' },

  { id: 'D2', who: 'duplo', voice: { pm_alex: 1 }, speed: 0.72, pitch: -3, fx: 'double_whisper',
    shot: 'black_whisper', at: 5.0, text: 'Agora podemos começar.' },
];
for (const l of LINES) l.t = T(l.shot, l.at);

// Eventos com significado narrativo e sonoro (som e imagem leem os mesmos tempos).
export const EVENTS = {
  lightning: [T('aerial_town', 6.2), T('road_to_house', 6.8), T('window_push', 8.6), T('car_exterior', 3.2), T('car_interior', 15.5)],
  silhouette: [T('window_push', 7.0), T('window_push', 9.4)],
  windowCut: T('window_push', 10.4),
  titleIn: T('title', 1.2),
  subtitleIn: T('title', 4.2),
  titleOut: T('title', 6.8),
  phoneBuzz: [T('apartment', 0.8), T('apartment', 2.0)],
  phoneAnswer: T('apartment', 3.4),
  radioStaticOn: T('car_interior', 4.5),
  radioLook: T('car_interior', 11.2),
  radioStaticOff: T('car_interior', 14.0),
  carStop: T('arrival', 6.5),
  rainStop: T('arrival', 9.0),
  carDoorOpen: T('arrival', 9.6),
  carDoorClose: T('arrival', 11.4),
  keyInsert: T('front_door', 4.2),
  keyTurn: T('front_door', 5.2),
  doorOpen: T('front_door', 6.2),
  flashlightOn: T('hallway', 0.8),
  breathOn: [T('hallway', 6.5), T('hallway', 15.6)],
  stopWalk: T('hallway', 11.8),
  breathOff: T('hallway', 12.0),
  resumeWalk: T('hallway', 15.0),
  albumOpen: T('album', 2.5),
  pageFlips: [T('album', 4.2), T('album', 6.0), T('album', 7.6), T('album', 9.4)],
  photoFound: T('album', 10.4),
  photoTurn: T('album', 15.6),
  clockStart: T('album', 25.6),
  clockChimes: Array.from({ length: 11 }, (_, i) => T('clock', 0.9 + i * 0.82)),
  thudUpstairs: T('stairs', 0.5),
  boxOpen: T('child_room', 8.6),
  knocks1: [T('knocks', 1.0), T('knocks', 1.75), T('knocks', 2.5)],
  doorReveal: T('basement_door', 2.0),
  knobTurn: T('basement_door', 7.8),
  stepBack: T('basement_door', 10.2),
  flicker: T('basement_door', 27.8),
  keyholeBreath: T('basement_door', 29.5),
  phoneRing: T('run_phone', 3.2),
  phoneAnswer2: T('run_phone', 7.0),
  turnToStairs: T('run_phone', 27.0),
  headTilt: T('double_reveal', 9.0),
  smile: T('double_reveal', 12.8),
  descendStart: T('decision', 0.6),
  freeze: T('decision', 13.6),
  knocks2: [T('black_whisper', 0.9), T('black_whisper', 1.6), T('black_whisper', 2.3)],
  deepBreath: T('black_whisper', 3.0),
  finalSound: T('end_card', 7.2),
};

// Passos de Gabriel (superfície) — gerados pelo animador e usados pelo áudio.
// Cada entrada: [tempo absoluto, superfície, intensidade]. Preenchido em steps.js.
