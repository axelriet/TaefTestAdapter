# Axel's editorial style

Copyright © NVIDIA Corporation. All rights reserved.

Author: Codex (editorial revision)
Date: October 12, 2026
Version: 2026-10-12 editorial revision

The owner requested this guide on 2026-10-06. It guides project prose;
contracts, settled decisions and exact code formatting remain authoritative.
Use it with the rulebook's exact-minimalism standard.

## Voice

Write as an experienced engineer explaining a system to another practitioner.
State the purpose, show the mechanism, and explain the consequence. Use concrete
nouns, active verbs, exact API names and useful numbers. Assume technical
competence; introduce unfamiliar GDS concepts before relying on them.

Be direct and confident where the evidence supports it. Identify uncertainty
where it affects a decision. Prefer a specific limit or tradeoff to a string
of adjectives. Give the reader control: explain which choice matters and why.

Address the reader as "you" when giving instructions. Use natural transitions
and short, coherent paragraphs. Vary sentence length; a short sentence can
land a consequence after a technical explanation. Cut ceremonial openings,
obvious parameter descriptions, repeated conclusions and decorative prose.

## Apply the voice by context

- Reference and code comments: state the purpose and non-obvious contract.
    Preserve ownership, lifetime, units, bounds, errors and exceptions once,
    where their scope is clear. Use plain statements and direct instructions.
- Tutorials and samples: start with a task, show the smallest correct example,
    then explain the result and the next useful variation. Let the code teach.
    Keep necessary error handling and cleanup; identify abbreviated fragments.
- Design and internal notes: state the decision, constraint, mechanism and
    consequence. Keep evidence, unresolved questions and recovery steps needed
    to continue work. Move superseded detail into history rather than repeating it.
- Project narratives and the post-mortem: begin with a concrete event or problem,
    establish the engineering constraints, and follow cause and effect to the
    outcome. Use dates and scale when they matter. First-person accounts require
    the owner's actual experience or an explicitly attributed account.

An analogy belongs where it makes a mechanism easier to understand. Keep it
brief and return to the engineering point. Conversational emphasis or dry
humor can suit a guide or narrative; a contract needs precise wording.

## Editing test

Each sentence must carry a contract or teach something the code cannot show.
Remove it if the reader loses nothing. Preserve both fidelity and room for
future evolution. Check what the caller must keep, do, expect or decide;
do not pad the text to satisfy a template.

For example:

> Cancellation requests an operation to stop. Keep its buffers valid until
> completion.

Check the exact contract before using an example. Match the voice without
importing another product's behavior, performance claims or code conventions.
Use proper American English and the project's required paragraph, indentation,
line-wrap and trailing-space rules.

## Simplified Technical English

Apply ASD-STE100 principles with the mode below. This is a practical application
of those principles. Do not claim full official ASD-STE100 compliance.

### Choose the document mode

- **API reference:** Use strict-leaning STE for reference text and contract comments.
- **User guide:** Use pragmatic 80% STE for user guides, concepts, overviews and design explanations.

Keep exact technical terms, including function names, types, protocols,
algorithms and metrics. Use one name for each concept.

### Rules for both modes

- Prefer active voice. Use passive voice when the actor is unknown or irrelevant.
- Give each sentence one main idea. Put the most important information first.
- Use concrete verbs and nouns. Prefer present tense for descriptions and imperative instructions.
- Target 20 words or fewer for procedural sentences and 25 for descriptive sentences.
- Avoid stacked modifiers, nominalizations, long compound sentences and unnecessary synonyms.
- Remove marketing language, vague adjectives and empty hedging.
- Preserve preconditions, guarantees, permissions, limits, exceptions, ownership and errors.
- Preserve genuine uncertainty and conditional behavior. Keep words such as "may" when they carry that meaning.
- Do not invent facts or actors, drop qualifiers, or change API behavior to simplify a sentence.

Sentence lengths are targets. Preserve meaning when a technical statement needs
more words. Split it only when each sentence retains a clear scope.

### API reference mode

Give procedures as short steps. Put one action in each sentence.
State preconditions, parameters, return values and error conditions explicitly.
Keep parameter and type descriptions concise and consistent.

Prefer "Call X with Y" and "The function returns Z" to long explanatory clauses.
Keep the information the caller needs to use the API correctly.

### User guide mode

Use short explanatory paragraphs. State rationale and tradeoffs when they help
the reader understand a choice. Use natural phrasing and a broader technical
vocabulary when they improve clarity. Keep the same naming and sentence targets.

### Review a rewrite

State the mode before presenting rewritten text. Add a short change note only
when it helps the review. Check the original and rewrite for the same technical
meaning. Preserve quoted decisions, source citations and evidence.
