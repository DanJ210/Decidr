import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '../views/HomeView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'home',
      component: HomeView,
    },
    {
      path: '/auth/callback',
      name: 'auth-callback',
      component: () => import('../views/AuthCallbackView.vue'),
    },
    {
      path: '/cases/:id',
      name: 'case-detail',
      component: () => import('../views/CaseDetailView.vue'),
      props: true,
    },
    {
      path: '/cases/new',
      name: 'case-create',
      component: () => import('../views/CreateCaseView.vue'),
    },
    {
      path: '/rewards',
      name: 'rewards',
      component: () => import('../views/RewardsView.vue'),
    },
    {
      path: '/friends',
      name: 'friends',
      component: () => import('../views/FriendsView.vue'),
    },
  ],
})

export default router
