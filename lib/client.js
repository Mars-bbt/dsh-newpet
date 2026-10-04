window.__ModuleLoader__.load({
  id: 'dsh-newpet',
  factory(require) {
    const React = require('react');
    const { jsx, jsxs } = require('react/jsx-runtime');
    const api = '/api/dsh-newpet/';
    const inputStyle = { width: 160, padding: '6px 10px', borderRadius: 8, border: '1px solid var(--dsw-alias-border-l2)', background: 'transparent', color: 'inherit' };
    const rowStyle = { display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 16, padding: '12px 0' };
    async function request(route) {
      const response = await fetch(api + route, { cache: 'no-store' });
      if (!response.ok) throw new Error('桌宠接口暂不可用');
      return response.json();
    }
    function Panel() {
      const [status, setStatus] = React.useState({ running: false, available: true });
      const [names, setNames] = React.useState({ title: '主人', selfName: '鲸鱼娘' });
      const [pending, setPending] = React.useState(false);
      const [error, setError] = React.useState('');
      React.useEffect(() => {
        let active = true;
        const sync = () => request('desktop-pet?status=1').then(value => { if (active) setStatus(value); }).catch(() => {});
        request('names').then(value => { if (active) setNames(value); }).catch(() => {});
        sync(); const timer = setInterval(sync, 4000);
        return () => { active = false; clearInterval(timer); };
      }, []);
      async function toggle() {
        setPending(true); setError('');
        try { setStatus(await request(`desktop-pet?on=${status.running ? 0 : 1}`)); }
        catch (failure) { setError(failure.message); }
        finally { setPending(false); }
      }
      function nameField(label, field, query) {
        return jsxs('label', { style: rowStyle, children: [jsx('span', { children: label }), jsx('input', {
          style: inputStyle, value: names[field], maxLength: 24,
          onChange: event => setNames({ ...names, [field]: event.target.value }),
          onBlur: () => request(`names?${query}=${encodeURIComponent(names[field])}`).then(setNames).catch(failure => setError(failure.message)),
        })] });
      }
      return jsxs('div', { style: { width: '100%' }, children: [
        jsxs('div', { style: rowStyle, children: [jsxs('div', { children: [
          jsx('div', { children: '桌面悬浮桌宠' }),
          jsx('small', { children: status.running ? '正在陪伴你，窗口隐藏后仍可互动' : '已关闭' }),
        ] }), jsx('button', { disabled: pending || !status.available, onClick: toggle,
          children: pending ? '请稍候' : status.running ? '关闭' : '启动' })] }),
        nameField('如何称呼我', 'title', 'title'), nameField('她的自称', 'selfName', 'self'),
        error ? jsx('p', { role: 'status', children: error }) : null,
      ] });
    }
    return { inject: ['slots'], apply(ctx) {
      const slots = ctx.get('slots');
      if (!slots) return;
      slots.inject('settings.section', () => slots.register({ name: 'settings.section', id: 'mascot',
        order: 6, label: '看板娘' }, () => jsx(Panel, {})));
    } };
  },
});
