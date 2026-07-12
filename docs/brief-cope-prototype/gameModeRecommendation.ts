import {
  CopeAnswers,
  CopeBucket,
  CopeSubscale,
  scoreBuckets,
  scoreSubscales,
  topSubscaleInBucket,
} from "./briefCopeData";

// The actual game only has 3 modes (confirmed against the Unity project:
// Assets/Scenes/Rage Room/, Assets/evococo/meditation.unity, Assets/b-o-o-k/BoxingMenu.unity).
export type GameMode =
  | "Boxing" // structured sparring, strategy rewarded
  | "RageRoom" // pure catharsis, hit stuff, no fail state
  | "Meditate"; // guided breathing, no pressure

export interface ModeRecommendation {
  mode: GameMode;
  modeName: string;
  coachMessage: string;
}

const MODE_INFO: Record<GameMode, { modeName: string; coachMessage: string }> = {
  Boxing: {
    modeName: "Boxing",
    coachMessage:
      "You're a planner. You size up a problem before you swing at it — that's real fighter instinct. " +
      "Let's get you into Boxing, where every match rewards strategy as much as power. Read your " +
      "opponent, build your gameplan, and work the fight your way.",
  },
  RageRoom: {
    modeName: "Rage Room",
    coachMessage:
      "Alright champ — looks like you deal with pressure by letting it out, not bottling it up. " +
      "That's exactly what the Rage Room's for: no scorecards, no losing, just you and every ounce " +
      "of stress you're ready to throw at something.",
  },
  Meditate: {
    modeName: "Meditate",
    coachMessage:
      "Hey — before any fighting, let's slow down for a second. Meditate isn't a step back, it's your " +
      "warm-up: guided breathing, no pressure, no clock. When you're ready to swing for real, the ring " +
      "will still be here.",
  },
};

const DISCLAIMER =
  "(This is just a suggestion based on how you said you've been coping lately — not a diagnosis. " +
  "Pick whatever mode you're actually in the mood for.)";

function pickContextMode(topSubscale: CopeSubscale): GameMode {
  if (topSubscale === "Religion") return "Meditate";
  return "RageRoom"; // Humor or Venting
}

export function recommendGameMode(answers: CopeAnswers): ModeRecommendation {
  const subscaleScores = scoreSubscales(answers);
  const bucketScores = scoreBuckets(subscaleScores);

  const topBucket = (Object.keys(bucketScores) as CopeBucket[]).reduce((best, bucket) =>
    bucketScores[bucket] > bucketScores[best] ? bucket : best
  );

  let mode: GameMode;
  if (topBucket === "Avoidant") {
    mode = "Meditate";
  } else if (topBucket === "Approach") {
    mode = "Boxing";
  } else {
    mode = pickContextMode(topSubscaleInBucket(subscaleScores, "Context"));
  }

  const info = MODE_INFO[mode];
  return {
    mode,
    modeName: info.modeName,
    coachMessage: `${info.coachMessage} ${DISCLAIMER}`,
  };
}
