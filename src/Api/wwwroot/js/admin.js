// Sidebar toggle (mobile)
const toggle  = document.getElementById('sidebarToggle');
const sidebar = document.getElementById('sidebar');

if (toggle && sidebar) {
  toggle.addEventListener('click', () => sidebar.classList.toggle('open'));
  document.addEventListener('click', e => {
    if (sidebar.classList.contains('open') && !sidebar.contains(e.target) && e.target !== toggle)
      sidebar.classList.remove('open');
  });
}
