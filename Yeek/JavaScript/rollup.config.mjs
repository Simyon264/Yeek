import { nodeResolve } from '@rollup/plugin-node-resolve';
import commonjs from '@rollup/plugin-commonjs';
import terser from '@rollup/plugin-terser';
import { visualizer } from 'rollup-plugin-visualizer';

export default {
    input: 'src/index.js',
    output: {
        file: '../wwwroot/interop.bundle.js',
        name: "interop",
        format: 'iife',
        inlineDynamicImports: true
    },
    plugins: [
        nodeResolve({
            browser: true,
            moduleDirectories: ['node_modules']
        }),
        //commonjs(),
        terser(),
        visualizer({ filename: './stats.html' })
    ],
    treeshake: {
        moduleSideEffects: false
    }
};