import json,os
d=os.path.dirname(os.path.abspath(__file__))+'/'
S=d+'../Assets/StoryContent/Resources/Stories/'
stories={f[:-6]:open(S+f).read() for f in sorted(os.listdir(S)) if f.endswith('.story')}
js='const STORIES = '+json.dumps(stories,ensure_ascii=False)+';\n'
for f in ['core.js','art.js','stage.js','audio.js','ui.js','minigames.js','player.js','main.js']:
    js+=open(d+'src/'+f).read()+'\n'
html=open(d+'src/shell.html').read().replace('/*CSS*/',open(d+'src/style.css').read()).replace('/*JS*/',js.replace('</script','<\\/script'))
open(d+'index.html','w').write(html)

print(len(html))
