import { createRouter, createWebHistory } from 'vue-router'
import CaseDetailView from '../views/CaseDetailView.vue'
import CreateCaseView from '../views/CreateCaseView.vue'
import HomeView from '../views/HomeView.vue'
import RewardsView from '../views/RewardsView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'home',
      component: HomeView,
    },
    {
      path: '/cases/:id',
      name: 'case-detail',
      component: CaseDetailView,
      props: true,
    },
    {
      path: '/cases/new',
      name: 'case-create',
      component: CreateCaseView,
    },
    {
      path: '/rewards',
      name: 'rewards',
      component: RewardsView,
    },
  ],
})

export default router
