#include "startwindow.h"
#include "ui_startwindow.h"
#include "Dungeonspawner.h"
#include "PlaySquare.h"


startwindow::startwindow(QWidget *parent) :
    QMainWindow(parent),
    ui(new Ui::startwindow)
{
    ui->setupUi(this);
    ui->stackedWidget->setCurrentIndex(0);
    ui->stackedWidget->isEnabled();
}

startwindow::~startwindow()
{
    delete ui;
}

void startwindow::on_StartButton_clicked()
{
    ui->stackedWidget->setCurrentIndex(1);
    ui->stackedWidget->isEnabled();
}

void startwindow::on_QuitButton_clicked()
{
    exit(0);
}

void startwindow::on_QuestionsBox_textChanged()
{

}

void startwindow::on_PointAwards_overflow()
{

}

void startwindow::on_NextQButton_clicked()
{
    ui->stackedWidget->setCurrentIndex(1);
    ui->stackedWidget->isEnabled();
}

void startwindow::on_QuitButton_2_clicked()
{
    exit(0);
}

void startwindow::on_GS_Creator_clicked()
{
    ui->stackedWidget->setCurrentIndex(1);
    ui->stackedWidget->isEnabled();
}

void startwindow::on_GS_Start_clicked()
{
    DungeonSpawner DMSGame;
    DMSGame.show();
}

void startwindow::on_GS_Profile_clicked()
{
    ui->stackedWidget->setCurrentIndex(3);
    ui->stackedWidget->isEnabled();
}

void startwindow::on_PR_Creator_clicked()
{
    ui->stackedWidget->setCurrentIndex(1);
    ui->stackedWidget->isEnabled();
}

void startwindow::on_PR_Play_clicked()
{
    DungeonSpawner DMSGame;
    DMSGame.show();
}

void startwindow::on_PR_Profile_clicked()
{
    ui->stackedWidget->setCurrentIndex(3);
    ui->stackedWidget->isEnabled();
}

void startwindow::on_CR_PlayScreen_clicked()
{
    ui->stackedWidget->setCurrentIndex(2);
    ui->stackedWidget->isEnabled();
}
