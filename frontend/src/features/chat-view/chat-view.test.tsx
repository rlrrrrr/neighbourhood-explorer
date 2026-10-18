import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import "../../app/locale-runtime";
import { ChatView } from "./chat-view";

function renderChat({ criteriaKeys }: { criteriaKeys: readonly string[] }) {
  return renderToStaticMarkup(
    <ChatView
      criteria={{ weights: criteriaKeys.map((metricKey) => ({ metricKey, weight: 1 })) }}
      onToggleCriterion={() => {}}
      onShowOnMap={() => {}}
    />,
  );
}

describe("chat view", () => {
  it("offers the sample criteria as toggle buttons, none pressed by default", () => {
    const markup = renderChat({ criteriaKeys: [] });

    expect(markup).toContain("Ajouter un critère");
    expect(markup.match(/aria-pressed="false"/g)).toHaveLength(3);
  });

  it("invites the visitor to describe a neighborhood while no criterion is set", () => {
    expect(renderChat({ criteriaKeys: [] })).toContain("Aucun critère pour l&#x27;instant");
  });

  it("presses the criteria handed over by the URL", () => {
    const markup = renderChat({ criteriaKeys: ["cafes"] });

    expect(markup.match(/aria-pressed="true"/g)).toHaveLength(1);
    expect(markup).not.toContain("Aucun critère pour l&#x27;instant");
  });

  it("keeps the sample results with their map action", () => {
    const markup = renderChat({ criteriaKeys: [] });

    expect(markup.match(/Voir sur la carte/g)).toHaveLength(3);
    expect(markup).toContain("Moda");
  });
});
