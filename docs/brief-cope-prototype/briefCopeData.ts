// Brief COPE (Carver, 1997) — 28 items, verbatim canonical wording.
// Source: Carver, C. S. (1997). International Journal of Behavioral Medicine, 4(1), 92-100.
//         https://www.psy.miami.edu/faculty/ccarver/brief-cope.html

export type CopeSubscale =
  | "SelfDistraction"
  | "ActiveCoping"
  | "Denial"
  | "SubstanceUse"
  | "EmotionalSupport"
  | "InstrumentalSupport"
  | "BehavioralDisengagement"
  | "Venting"
  | "PositiveReframing"
  | "Planning"
  | "Humor"
  | "Acceptance"
  | "Religion"
  | "SelfBlame";

export interface CopeQuestion {
  id: number; // 1-28, matches original Brief COPE item numbering
  text: string;
  subscale: CopeSubscale;
}

export type CopeAnswer = 1 | 2 | 3 | 4;
export type CopeAnswers = Record<number, CopeAnswer>;

export const RESPONSE_SCALE: { value: CopeAnswer; label: string }[] = [
  { value: 1, label: "I haven't been doing this at all" },
  { value: 2, label: "I've been doing this a little bit" },
  { value: 3, label: "I've been doing this a medium amount" },
  { value: 4, label: "I've been doing this a lot" },
];

export const COPE_QUESTIONS: CopeQuestion[] = [
  { id: 1, text: "I've been turning to work or other activities to take my mind off things.", subscale: "SelfDistraction" },
  { id: 2, text: "I've been concentrating my efforts on doing something about the situation I'm in.", subscale: "ActiveCoping" },
  { id: 3, text: "I've been saying to myself \"this isn't real.\"", subscale: "Denial" },
  { id: 4, text: "I've been using alcohol or other drugs to make myself feel better.", subscale: "SubstanceUse" },
  { id: 5, text: "I've been getting emotional support from others.", subscale: "EmotionalSupport" },
  { id: 6, text: "I've been giving up trying to deal with it.", subscale: "BehavioralDisengagement" },
  { id: 7, text: "I've been taking action to try to make the situation better.", subscale: "ActiveCoping" },
  { id: 8, text: "I've been refusing to believe that it has happened.", subscale: "Denial" },
  { id: 9, text: "I've been saying things to let my unpleasant feelings escape.", subscale: "Venting" },
  { id: 10, text: "I've been getting help and advice from other people.", subscale: "InstrumentalSupport" },
  { id: 11, text: "I've been using alcohol or other drugs to help me get through it.", subscale: "SubstanceUse" },
  { id: 12, text: "I've been trying to see it in a different light, to make it seem more positive.", subscale: "PositiveReframing" },
  { id: 13, text: "I've been criticizing myself.", subscale: "SelfBlame" },
  { id: 14, text: "I've been trying to come up with a strategy about what to do.", subscale: "Planning" },
  { id: 15, text: "I've been getting comfort and understanding from someone.", subscale: "EmotionalSupport" },
  { id: 16, text: "I've been giving up the attempt to cope.", subscale: "BehavioralDisengagement" },
  { id: 17, text: "I've been looking for something good in what is happening.", subscale: "PositiveReframing" },
  { id: 18, text: "I've been making jokes about it.", subscale: "Humor" },
  { id: 19, text: "I've been doing something to think about it less, such as going to movies, watching TV, reading, daydreaming, sleeping, or shopping.", subscale: "SelfDistraction" },
  { id: 20, text: "I've been accepting the reality of the fact that it has happened.", subscale: "Acceptance" },
  { id: 21, text: "I've been expressing my negative feelings.", subscale: "Venting" },
  { id: 22, text: "I've been trying to find comfort in my religion or spiritual beliefs.", subscale: "Religion" },
  { id: 23, text: "I've been trying to get advice or help from other people about what to do.", subscale: "InstrumentalSupport" },
  { id: 24, text: "I've been learning to live with it.", subscale: "Acceptance" },
  { id: 25, text: "I've been thinking hard about what steps to take.", subscale: "Planning" },
  { id: 26, text: "I've been blaming myself for things that happened.", subscale: "SelfBlame" },
  { id: 27, text: "I've been praying or meditating.", subscale: "Religion" },
  { id: 28, text: "I've been making fun of the situation.", subscale: "Humor" },
];

const ALL_SUBSCALES: CopeSubscale[] = [
  "SelfDistraction", "ActiveCoping", "Denial", "SubstanceUse", "EmotionalSupport",
  "InstrumentalSupport", "BehavioralDisengagement", "Venting", "PositiveReframing",
  "Planning", "Humor", "Acceptance", "Religion", "SelfBlame",
];

export function scoreSubscales(answers: CopeAnswers): Record<CopeSubscale, number> {
  const totals = Object.fromEntries(ALL_SUBSCALES.map((s) => [s, 0])) as Record<CopeSubscale, number>;
  for (const q of COPE_QUESTIONS) {
    totals[q.subscale] += answers[q.id] ?? 0;
  }
  return totals;
}

export type CopeBucket = "Approach" | "Avoidant" | "Context";

const BUCKET_BY_SUBSCALE: Record<CopeSubscale, CopeBucket> = {
  ActiveCoping: "Approach",
  Planning: "Approach",
  PositiveReframing: "Approach",
  Acceptance: "Approach",
  EmotionalSupport: "Approach",
  InstrumentalSupport: "Approach",
  Denial: "Avoidant",
  SubstanceUse: "Avoidant",
  BehavioralDisengagement: "Avoidant",
  SelfDistraction: "Avoidant",
  SelfBlame: "Avoidant",
  Humor: "Context",
  Religion: "Context",
  Venting: "Context",
};

export function scoreBuckets(subscaleScores: Record<CopeSubscale, number>): Record<CopeBucket, number> {
  const totals: Record<CopeBucket, number> = { Approach: 0, Avoidant: 0, Context: 0 };
  for (const subscale of ALL_SUBSCALES) {
    totals[BUCKET_BY_SUBSCALE[subscale]] += subscaleScores[subscale];
  }
  return totals;
}

export function topSubscaleInBucket(
  subscaleScores: Record<CopeSubscale, number>,
  bucket: CopeBucket
): CopeSubscale {
  const candidates = ALL_SUBSCALES.filter((s) => BUCKET_BY_SUBSCALE[s] === bucket);
  return candidates.reduce((best, s) => (subscaleScores[s] > subscaleScores[best] ? s : best), candidates[0]);
}
