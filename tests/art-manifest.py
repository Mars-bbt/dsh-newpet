"""Inspect selected PNGs and record provenance; this script never edits pixels."""
import hashlib
import json
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parent.parent
poses = {
    'idle': ('exec-bf5bd369-e477-4338-80cf-874809c18072.png', '待机，站立微笑，围裙上添加可爱的鲸鱼图案。'),
    'work': ('exec-cc1779dd-dc50-401d-9e2f-eb93435ac963.png', '坐着打字，笔记本背面和围裙都有鲸鱼图案；头低下，眼睛朝电脑屏幕看。'),
    'thinking': ('exec-d2e1fd0a-536a-462d-9443-19edce05edf9.png', '思考，手指轻放下巴。'),
    'success': ('exec-3bb04a20-9524-486b-8b53-7b64b1b547e0.png', '成功，微笑并竖起拇指。'),
    'failure': ('exec-f07fe338-c929-46f0-90f6-fc5be25c3a44.png', '出错，担心的表情，双手在胸前。'),
    'celebrate': ('exec-ef42d647-2f9c-458a-b93b-ec05bd5fcecd.png', '庆祝，双手举起，快乐地抬起一条腿。'),
    'sleep': ('exec-9ddb0d24-6e9d-4593-bd64-32e561d2940e.png', '睡觉，闭眼抱着鲸鱼玩偶。'),
    'interact': ('exec-a83d6f2c-5576-4ba5-98d5-6ce73ef30c70.png', '互动，挥手并眨眼。'),
}
manifest = {
    'author': 'Mars', 'license': 'MIT', 'tool': 'OpenAI built-in image_gen',
    'generatedOn': '2026-10-04', 'oldProjectArtworkUsed': False,
    'promptSet': {
        'characterSpecification': '原创可爱 Q 版人形鲸鱼娘，深蓝色头发、深蓝色女仆装、白色围裙、鲸鱼发饰和尾巴。保持各姿态同一角色，完整身体，透明背景，无文字或水印。',
        'variantInvariants': '以本次新生成的待机角色为参考，保留人物身份、深蓝色配色、服装、鲸鱼围裙图案和绘画风格，只改变指定的姿态和表情。',
        'workFinalEditVerbatim': 'Local precise edit of EYES AND HEAD ONLY. This girl is working at her laptop. She must visibly look at the laptop SCREEN, located BELOW and to the RIGHT in the image. Currently she appears to look at the viewer; FIX THAT. Move both irises and pupils far toward the LOWER RIGHT corners of their eye openings, creating a clear downward-right gaze, with more white visible above and to the left. Lower the upper eyelids slightly for focused eyes, do not close them. Turn face three-quarter toward the laptop (image right) and tip chin down a little. Sightline must point into the screen immediately above her typing hands, not toward the audience. Preserve every other feature exactly: navy maid dress, navy hair, whale hair clip, whale apron emblem, laptop whale emblem, hands typing, seated pose, full body, transparent background. No text. Do not redesign character.',
    }, 'assets': [],
}
for pose, (source, prompt) in poses.items():
    file = root / 'assets' / 'generated' / (pose + '.png')
    with Image.open(file) as image:
        assert image.mode == 'RGBA', (pose, image.mode)
        alpha = image.getchannel('A')
        assert alpha.getextrema()[0] == 0 and alpha.getextrema()[1] >= 250, (pose, alpha.getextrema())
        manifest['assets'].append({
            'pose': pose, 'file': f'generated/{pose}.png', 'generationFile': source,
            'sha256': hashlib.sha256(file.read_bytes()).hexdigest(),
            'dimensions': list(image.size), 'transparent': True, 'poseSpecification': prompt,
        })
(root / 'assets' / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('PASS: all eight assets have real alpha transparency; manifest saved.')
