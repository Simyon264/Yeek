import { EditorView, basicSetup } from "codemirror"
import { keymap } from "@codemirror/view"
import { indentWithTab, defaultKeymap } from "@codemirror/commands"
import { placeholder } from "@codemirror/view";
import { oneDark } from "@codemirror/theme-one-dark"
import {javascript} from "@codemirror/lang-javascript";

window.initializeEditor = function (domId) {
    const parent = document.getElementById(domId);
    if (!parent) {
        console.error("Failed to initialize editor, element not found: ", domId);
        return null;
    }

    const minHeightEditor = EditorView.theme({
        ".cm-content, .cm-gutter": {minHeight: "200px"}
    })

    let view = new EditorView({
        doc: "// This function is called automatically.\nasync function run() {\n  await log.info(\"Hello!\");\n  return 0;\n};",
        extensions: [
            basicSetup,
            javascript(),
            placeholder("Click to start typing... For JS reference, see above."),
            oneDark,
            minHeightEditor,
            keymap.of([defaultKeymap, indentWithTab])
        ],
        parent: parent,
    })

    console.debug("Initialized Editor: ", domId, view);
    return view;
}